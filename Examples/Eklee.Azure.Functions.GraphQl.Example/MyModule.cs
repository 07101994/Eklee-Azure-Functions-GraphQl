﻿using Autofac;
using Eklee.Azure.Functions.GraphQl.Example.BusinessLayer;
using Eklee.Azure.Functions.Http;
using Microsoft.Extensions.Caching.Distributed;

namespace Eklee.Azure.Functions.GraphQl.Example
{
	public class MyModule : Module
	{
		protected override void Load(ContainerBuilder builder)
		{
			builder.UseDistributedCache<MemoryDistributedCache>();

			builder.RegisterGraphQl<BooksSchema>();
			builder.RegisterType<BooksQuery>();
			builder.RegisterType<BooksMutation>();

			builder.RegisterType<InMemoryDbRepository>().As<IGraphQlRepository>().SingleInstance();
			builder.RegisterType<MyStartup>().As<IStartable>().SingleInstance();
		}
	}
}