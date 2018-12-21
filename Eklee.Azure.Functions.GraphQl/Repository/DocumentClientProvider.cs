﻿using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using GraphQL;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using Newtonsoft.Json;

namespace Eklee.Azure.Functions.GraphQl.Repository
{
	public class DocumentTypeInfo
	{
		public PartitionKeyDefinition Partition { get; set; }
		public int RequestUnit { get; set; }
		public string Id { get; set; }
	}

	public class DocumentClientProvider
	{
		private const int DefaultRequestUnits = 400;
		private readonly DocumentClient _documentClient;
		private readonly Dictionary<string, string> _databases = new Dictionary<string, string>();
		private readonly Dictionary<string, MemberExpression> _memberExpressions =
			new Dictionary<string, MemberExpression>();
		private readonly Dictionary<string, DocumentTypeInfo> _documentTypeInfos = new Dictionary<string, DocumentTypeInfo>();

		public DocumentClientProvider(string url, string key)
		{
			_documentClient = new DocumentClient(new Uri(url), key);
		}

		public async Task ConfigureDatabaseAndCollection(Dictionary<string, object> configurations, Type sourceType)
		{
			int requestUnit = DefaultRequestUnits;

			var databaseId = configurations.GetStringValue(DocumentDbConstants.Database, sourceType);

			if (configurations.ContainsKey(DocumentDbConfigurationExtensions.GetKey(DocumentDbConstants.RequestUnit, sourceType)))
			{
				int.TryParse(configurations.GetStringValue(DocumentDbConstants.RequestUnit, sourceType), out requestUnit);
			}

			if (!_databases.ContainsKey(databaseId))
			{
				await _documentClient.CreateDatabaseIfNotExistsAsync(
						new Database { Id = databaseId },
						new RequestOptions { OfferThroughput = requestUnit });
			}

			_databases[sourceType.Name.ToLower()] = databaseId;

			var documentTypeInfo = new DocumentTypeInfo
			{
				Id = sourceType.Name.ToLower(),
				Partition = configurations.GetValue<PartitionKeyDefinition>(DocumentDbConstants.Partition, sourceType),
				RequestUnit = requestUnit
			};

			await _documentClient.CreateDocumentCollectionIfNotExistsAsync(
				UriFactory.CreateDatabaseUri(databaseId), new DocumentCollection
				{
					Id = documentTypeInfo.Id,
					PartitionKey = documentTypeInfo.Partition
				}, new RequestOptions
				{
					OfferThroughput = documentTypeInfo.RequestUnit
				});

			_documentTypeInfos.Add(sourceType.Name, documentTypeInfo);

			var memberExpressionKey = DocumentDbConfigurationExtensions.GetKey(DocumentDbConstants.MemberExpression, sourceType);

			if (configurations.ContainsKey(memberExpressionKey))
				_memberExpressions.Add(sourceType.Name, (MemberExpression)configurations[memberExpressionKey]);
		}

		public async Task CreateAsync<T>(T item)
		{
			await _documentClient.CreateDocumentAsync(GetDocumentCollectionUri<T>(), GetTransformed(item), null, true);
		}

		private dynamic GetTransformed<T>(T item)
		{
			dynamic expando = JsonConvert.DeserializeObject<ExpandoObject>(
				JsonConvert.SerializeObject(item));

			expando.id = item.GetKey();

			return expando;
		}

		private Uri GetDocumentCollectionUri<T>()
		{
			var collectionName = GetCollectionName<T>();
			var databaseId = _databases[collectionName];

			return UriFactory.CreateDocumentCollectionUri(databaseId, collectionName);
		}

		private Uri GetDocumentUri<T>(T item)
		{
			var collectionName = GetCollectionName<T>();
			var databaseId = _databases[collectionName];

			return UriFactory.CreateDocumentUri(databaseId, collectionName, item.GetKey());
		}

		private string GetCollectionName<T>()
		{
			return typeof(T).Name.ToLower();
		}

		public async Task UpdateAsync<T>(T item)
		{
			await _documentClient.ReplaceDocumentAsync(GetDocumentUri(item), GetTransformed(item));
		}

		public async Task DeleteAsync<T>(T item)
		{
			PartitionKey partitionKey = null;
			if (_memberExpressions.ContainsKey(typeof(T).Name))
			{
				var memberExpression = _memberExpressions[typeof(T).Name];
				var value = item.GetPropertyValue(memberExpression.Member.Name);
				partitionKey = new PartitionKey(value);
			}

			await _documentClient.DeleteDocumentAsync(GetDocumentUri(item), new RequestOptions
			{
				PartitionKey = partitionKey
			});
		}

		public Task<IEnumerable<T>> QueryAsync<T>(IEnumerable<QueryParameter> queryParameters)
		{
			var sql = $"SELECT * FROM {typeof(T).Name} x WHERE ";

			queryParameters.ToList().ForEach(x => { sql += TranslateQueryParameter(x); });

			return Task.FromResult<IEnumerable<T>>(_documentClient.CreateDocumentQuery<T>(GetDocumentCollectionUri<T>(), sql, new FeedOptions { PartitionKey = new PartitionKey("") }));
		}

		private string TranslateQueryParameter(QueryParameter queryParameter)
		{
			string comparison;
			switch (queryParameter.Comparison)
			{
				case Comparisons.Equals:
					comparison = "=";
					break;

				default:
					throw new NotImplementedException($"Comparison {queryParameter.Comparison} is not impleted.");
			}

			return $" x.{queryParameter.MemberModel.Name} {comparison} '{queryParameter.ContextValue.Value}'";
		}

		public async Task DeleteAllAsync<T>()
		{
			var documentTypeInfo = _documentTypeInfos[typeof(T).Name];

			var uri = UriFactory.CreateDocumentCollectionUri(_databases[typeof(T).Name.ToLower()], documentTypeInfo.Id);

			var options = new FeedOptions
			{
				MaxItemCount = 100
			};

			while (true)
			{
				var query = _documentClient.CreateDocumentQuery<T>(uri, options).AsDocumentQuery();

				var response = await query.ExecuteNextAsync<T>();

				if (response.Count == 0) break;

				foreach (var item in response)
				{
					await DeleteAsync(item);
				}

				if (string.IsNullOrEmpty(response.ResponseContinuation)) break;

				options.RequestContinuation = response.ResponseContinuation;
			}
		}
	}
}