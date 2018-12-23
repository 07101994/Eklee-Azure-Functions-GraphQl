﻿using System;
using System.Collections.Generic;
using Eklee.Azure.Functions.GraphQl.Filters;
using GraphQL.Builders;
using GraphQL.Types;

namespace Eklee.Azure.Functions.GraphQl
{
	public static class QueryParameterBuilderExtensions
	{
		public static QueryArguments GetQueryArguments<TSource>(this QueryParameterBuilder<TSource> queryParameterBuilder)
		{
			var queryArguments = new List<QueryArgument>();

			queryParameterBuilder.ForEach(modelMember =>
			{
				if (modelMember.IsString)
				{
					if (modelMember.IsOptional)
					{
						queryArguments.Add(new QueryArgument<ModelConventionInputType<StringFilter>>
						{
							Name = modelMember.Name,
							Description = modelMember.Description
						});
					}
					else
					{
						queryArguments.Add(new QueryArgument<NonNullGraphType<ModelConventionInputType<StringFilter>>>
						{
							Name = modelMember.Name,
							Description = modelMember.Description
						});
					}

					return;
				}

				throw new NotImplementedException($"QueryArgument type is not yet implemented for {modelMember.Name}");
			});


			return new QueryArguments(queryArguments);
		}

		public static void PopulateWithArguments<TSource>(this QueryParameterBuilder<TSource> queryParameterBuilder,
			ConnectionBuilder<ModelConventionType<TSource>, object> connectionBuilder)
		{
			queryParameterBuilder.ForEach(modelMember =>
			{
				if (modelMember.IsString)
				{
					connectionBuilder = modelMember.IsOptional ?
						connectionBuilder.Argument<ModelConventionInputType<StringFilter>>(modelMember.Name, modelMember.Description) :
						connectionBuilder.Argument<NonNullGraphType<ModelConventionInputType<StringFilter>>>(modelMember.Name, modelMember.Description);
				}
				else
				{
					throw new NotImplementedException($"QueryArgument type is not yet implemented for {modelMember.Name}");
				}
			});
		}
	}
}
