using Autofac;
using DbfProcessor.Core;

namespace DbfProcessor
{
    class Program
    {
        static void Main()
        {
            using var scope = ContainerInitializer.Initialize().BeginLifetimeScope();
            var app = scope.Resolve<Application>();
            app.Run();
        }
    }
}
