using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker.Converters;

namespace Microsoft.Azure.Functions.Worker.Extensions.ServiceBus
{
    public class ServiceBusMessageLite
    {
        public IEnumerable<string> Items { set; get; } = Array.Empty<string>();

        public string? Label { set; get; }

        public string? Subject { set; get; }
    }

    internal class MyPrimaryConverter : IInputConverter
    {
        public ValueTask<ConversionResult> ConvertAsync(ConverterContext context)
        {
            if (context.TargetType != typeof(ServiceBusMessageLite))
            {
                return new ValueTask<ConversionResult>(ConversionResult.Unhandled());
            }

            if (context.Source is not IEnumerable<string> itemsArray)
            {
                return new ValueTask<ConversionResult>(ConversionResult.Unhandled());
            }
            var msg = new ServiceBusMessageLite
            {
                Items = itemsArray,
                Label = "Populated from Primary converter"
            };

            PopulatedMetaProperties(context, msg);

            return new ValueTask<ConversionResult>(ConversionResult.Success(msg));
        }

        internal static void PopulatedMetaProperties(ConverterContext context, ServiceBusMessageLite msg)
        {
            if (context.FunctionContext.BindingContext.BindingData.TryGetValue("Subject", out var subjectObj)
                            && subjectObj is string subject)
            {
                msg.Subject = subject;
            }
        }
    }

    internal class MyBackupConverter : MyPrimaryConverter, IInputConverter
    {
        public new ValueTask<ConversionResult> ConvertAsync(ConverterContext context)
        {
            if (context.TargetType != typeof(ServiceBusMessageLite))
            {
                return new ValueTask<ConversionResult>(ConversionResult.Unhandled());
            }

            if (context.Source is not string itemsAsString)
            {
                return new ValueTask<ConversionResult>(ConversionResult.Unhandled());
            }
            var msg = new ServiceBusMessageLite
            {
                Items = new string[] { itemsAsString },
                Label = "Populated from Backup converter"
            };

            PopulatedMetaProperties(context, msg);

            return new ValueTask<ConversionResult>(ConversionResult.Success(msg));
        }
    }
}
