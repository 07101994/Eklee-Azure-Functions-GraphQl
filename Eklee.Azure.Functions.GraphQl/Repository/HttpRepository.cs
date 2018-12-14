﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Eklee.Azure.Functions.GraphQl.Repository
{
	public class HttpRepository : IGraphQlRepository
	{
		private readonly HttpClient _httpClient = new HttpClient();

		private readonly Dictionary<string, HttpTypeConfiguration> _httpTypeConfigurations
			= new Dictionary<string, HttpTypeConfiguration>();

		private readonly Dictionary<string, Func<object, HttpResource>> _addTransforms =
			new Dictionary<string, Func<object, HttpResource>>();

		private readonly Dictionary<string, Func<object, HttpResource>> _updateTransforms =
			new Dictionary<string, Func<object, HttpResource>>();

		private readonly Dictionary<string, Func<object, HttpResource>> _deleteTransforms =
			new Dictionary<string, Func<object, HttpResource>>();

		private readonly Dictionary<string, Func<Dictionary<string, string>, HttpQueryResource>> _queryTransforms =
			new Dictionary<string, Func<Dictionary<string, string>, HttpQueryResource>>();

		public void Configure(Type sourceType, Dictionary<string, string> configurations)
		{
			// ReSharper disable once AssignNullToNotNullAttribute
			_httpTypeConfigurations.Add(sourceType.FullName, new HttpTypeConfiguration
			{
				BaseUrl = configurations[Constants.BaseUrl]
			});
		}

		private HttpClient GetHttpClient<T>()
		{
			// ReSharper disable once AssignNullToNotNullAttribute
			var configuration = _httpTypeConfigurations[typeof(T).FullName];
			_httpClient.BaseAddress = new Uri(configuration.BaseUrl);
			return _httpClient;
		}

		public async Task BatchAddAsync<T>(IEnumerable<T> items)
		{
			foreach (var item in items.ToList())
			{
				await AddAsync(item);
			}
		}

		public async Task AddAsync<T>(T item)
		{
			// ReSharper disable once AssignNullToNotNullAttribute
			var resource = _addTransforms[typeof(T).FullName](item);
			await InternalSendAsync(item, resource);
		}

		public void SetAddTransform(Type sourceType, Func<object, HttpResource> transform)
		{
			// ReSharper disable once AssignNullToNotNullAttribute
			_addTransforms.Add(sourceType.FullName, transform);
		}

		public void SetUpdateTransform(Type sourceType, Func<object, HttpResource> transform)
		{
			// ReSharper disable once AssignNullToNotNullAttribute
			_updateTransforms.Add(sourceType.FullName, transform);
		}

		public void SetDeleteTransform(Type sourceType, Func<object, HttpResource> transform)
		{
			// ReSharper disable once AssignNullToNotNullAttribute
			_deleteTransforms.Add(sourceType.FullName, transform);
		}

		public void SetQueryTransform(Type sourceType, Func<Dictionary<string, string>, HttpQueryResource> transform)
		{
			// ReSharper disable once AssignNullToNotNullAttribute
			_queryTransforms.Add(sourceType.FullName, transform);
		}

		private async Task InternalSendAsync<T>(T item, HttpResource resource)
		{
			var httpClient = GetHttpClient<T>();

			var content = resource.ContainsBody == false ? null :
				new StringContent(JsonConvert.SerializeObject(item),
					Encoding.UTF8, "application/json");

			var request = new HttpRequestMessage(resource.Method, resource.AppendUrl) { Content = content };
			var response = await httpClient.SendAsync(request);

			response.EnsureSuccessStatusCode();
		}

		public async Task UpdateAsync<T>(T item)
		{
			// ReSharper disable once AssignNullToNotNullAttribute
			var resource = _updateTransforms[typeof(T).FullName](item);
			await InternalSendAsync(item, resource);
		}

		public async Task DeleteAsync<T>(T item)
		{
			// ReSharper disable once AssignNullToNotNullAttribute
			var resource = _deleteTransforms[typeof(T).FullName](item);

			if (!resource.ContainsBody.HasValue) resource.ContainsBody = false;

			await InternalSendAsync(item, resource);
		}

		public async Task<IEnumerable<T>> QueryAsync<T>(string queryName, IEnumerable<QueryParameter> queryParameters)
		{
			var parameters = queryParameters.ToList();

			Dictionary<string, string> items =
				parameters.ToDictionary(x => x.MemberModel.Name, x => x.ContextValue.Value.ToString());

			// ReSharper disable once AssignNullToNotNullAttribute
			var resourceQuery = _queryTransforms[typeof(T).FullName](items);

			var httpClient = GetHttpClient<T>();

			HttpResponseMessage response;
			switch (resourceQuery.QueryType)
			{
				case HttpQueryTypes.AppendToUrl:
					response = await httpClient.GetAsync(resourceQuery.AppendUrl);
					break;

				default:
					throw new NotImplementedException();
			}

			response.EnsureSuccessStatusCode();

			var result = await response.Content.ReadAsStringAsync();

			return resourceQuery.IsListResult
				? JsonConvert.DeserializeObject<List<T>>(result)
				: new List<T> { JsonConvert.DeserializeObject<T>(result) };
		}
	}
}
