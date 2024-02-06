using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlanningCenterSDK.Apps.Services.Model
{
    public enum EmailTemplateFormat
    {
        Html,
        Text
    }

    public static class EmailTemplateFormatExtensions
    {
        public static string GetValue( this EmailTemplateFormat format )
        {
            switch (format)
            {
                case EmailTemplateFormat.Html:
                    return "html";
                case EmailTemplateFormat.Text:
                    return "text";
                default:
                    throw new ArgumentOutOfRangeException( nameof( format ), format, null );
            }
        }
    }
}
