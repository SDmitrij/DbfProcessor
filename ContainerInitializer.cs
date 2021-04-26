using Autofac;
using DbfProcessor.Core;
using DbfProcessor.Core.Storage;
using DbfProcessor.Out;

namespace DbfProcessor
{
    public static class ContainerInitializer
    {
        public static IContainer Initialize()
        {
            var builder = new ContainerBuilder();

            builder.RegisterType<Logging>()
                .AsSelf()
                .SingleInstance();
            builder.RegisterType<Impersonation>()
                .AsSelf()
                .SingleInstance();
            builder.RegisterType<Config>()
                .AsSelf()
                .SingleInstance();
            builder.RegisterType<Application>();
            builder.RegisterType<Exchange>();
            builder.RegisterType<Extract>();
            builder.RegisterType<Interaction>();
            builder.RegisterType<QueryBuild>();

            return builder.Build();
        }
    }
}
