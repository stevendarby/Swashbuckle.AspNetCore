using Microsoft.OpenApi.Models;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml.XPath;

namespace Swashbuckle.AspNetCore.SwaggerGen
{
    public class XmlCommentsRequestBodyFilter : IRequestBodyFilter
    {
        private readonly Dictionary<string, XPathNavigator> _docMembers;

        public XmlCommentsRequestBodyFilter(XPathDocument xmlDoc)
        {
            _docMembers = xmlDoc.CreateNavigator()
                .Select("/doc/members/member")
                .OfType<XPathNavigator>()
                .ToDictionary(memberNode => memberNode.GetAttribute("name", ""));
        }

        public void Apply(OpenApiRequestBody requestBody, RequestBodyFilterContext context)
        {
            var bodyParameterDescription = context.BodyParameterDescription;

            if (bodyParameterDescription == null) return;

            var propertyInfo = bodyParameterDescription.PropertyInfo();
            if (propertyInfo != null)
            {
                ApplyPropertyTags(requestBody, context, propertyInfo); return;
            }

            var parameterInfo = bodyParameterDescription.ParameterInfo();
            if (parameterInfo != null)
            {
                ApplyParamTags(requestBody, context, parameterInfo);
            }
        }

        private void ApplyPropertyTags(OpenApiRequestBody requestBody, RequestBodyFilterContext context, MemberInfo propertyInfo)
        {
            var propertyMemberName = XmlCommentsNodeNameHelper.GetMemberNameForFieldOrProperty(propertyInfo);

            if (!_docMembers.TryGetValue(propertyMemberName, out var propertyNode)) return;

            var summaryNode = propertyNode.SelectSingleNode("summary");
            if (summaryNode != null)
                requestBody.Description = XmlCommentsTextHelper.Humanize(summaryNode.InnerXml);

            var exampleNode = propertyNode.SelectSingleNode("example");
            if (exampleNode == null) return;

            foreach (var mediaType in requestBody.Content.Values)
            {
                var exampleAsJson = (mediaType.Schema?.ResolveType(context.SchemaRepository) == "string")
                    ? $"\"{exampleNode.ToString()}\""
                    : exampleNode.ToString();

                mediaType.Example = OpenApiAnyFactory.CreateFromJson(exampleAsJson);
            }
        }

        private void ApplyParamTags(OpenApiRequestBody requestBody, RequestBodyFilterContext context, ParameterInfo parameterInfo)
        {
            if (!(parameterInfo.Member is MethodInfo methodInfo))
                return;

            // If method is from a constructed generic type, look for comments from the generic type method
            var targetMethod = methodInfo.DeclaringType.IsConstructedGenericType
                ? methodInfo.GetUnderlyingGenericTypeMethod()
                : methodInfo;

            if (targetMethod == null) return;

            var methodMemberName = XmlCommentsNodeNameHelper.GetMemberNameForMethod(targetMethod);

            if (!_docMembers.TryGetValue(methodMemberName, out var propertyNode)) return;

            var paramNode = propertyNode.SelectSingleNode($"param[@name='{parameterInfo.Name}']");

            if (paramNode != null)
            {
                requestBody.Description = XmlCommentsTextHelper.Humanize(paramNode.InnerXml);

                var example = paramNode.GetAttribute("example", "");
                if (string.IsNullOrEmpty(example)) return;

                foreach (var mediaType in requestBody.Content.Values)
                {
                    var exampleAsJson = (mediaType.Schema?.ResolveType(context.SchemaRepository) == "string")
                        ? $"\"{example}\""
                        : example;

                    mediaType.Example = OpenApiAnyFactory.CreateFromJson(exampleAsJson);
                }
            }
        }
    }
}
