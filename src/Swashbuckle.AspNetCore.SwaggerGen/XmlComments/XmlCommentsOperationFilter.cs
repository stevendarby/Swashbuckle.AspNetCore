using System;
using System.Reflection;
using System.Xml.XPath;
using Microsoft.OpenApi.Models;

namespace Swashbuckle.AspNetCore.SwaggerGen
{
    public class XmlCommentsOperationFilter : IOperationFilter
    {
        private readonly Dictionary<string, XPathNavigator> _docMembers;

        public XmlCommentsOperationFilter(XPathDocument xmlDoc)
        {
            _docMembers = new Dictionary<string, XPathNavigator>();
            foreach (XPathNavigator memberNode in xmlDoc.CreateNavigator().Select("/doc/members/member"))
            {
                var memberName = memberNode.GetAttribute("name", "");
                _docMembers[memberName] = memberNode;
            }
        }

        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            if (context.MethodInfo == null) return;

            // If method is from a constructed generic type, look for comments from the generic type method
            var targetMethod = context.MethodInfo.DeclaringType.IsConstructedGenericType
                ? context.MethodInfo.GetUnderlyingGenericTypeMethod()
                : context.MethodInfo;

            if (targetMethod == null) return;

            ApplyControllerTags(operation, targetMethod.DeclaringType);
            ApplyMethodTags(operation, targetMethod);
        }

        private void ApplyControllerTags(OpenApiOperation operation, Type controllerType)
        {
            var typeMemberName = XmlCommentsNodeNameHelper.GetMemberNameForType(controllerType);

            if (!_docMembers.TryGetValue(typeMemberName, out var methodNode)) return;

            var responseNodes = methodNode.Select("response");
            if (responseNodes != null)
            {
                ApplyResponseTags(operation, responseNodes);
            }
        }

        private void ApplyMethodTags(OpenApiOperation operation, MethodInfo methodInfo)
        {
            var methodMemberName = XmlCommentsNodeNameHelper.GetMemberNameForMethod(methodInfo);

            if (!_docMembers.TryGetValue(methodMemberName, out var methodNode)) return;

            var summaryNode = methodNode.SelectSingleNode("summary");
            if (summaryNode != null)
                operation.Summary = XmlCommentsTextHelper.Humanize(summaryNode.InnerXml);

            var remarksNode = methodNode.SelectSingleNode("summary");
            if (remarksNode != null)
                operation.Description = XmlCommentsTextHelper.Humanize(remarksNode.InnerXml);

            var responseNodes = methodNode.Select("response");
            ApplyResponseTags(operation, responseNodes);
        }

        private void ApplyResponseTags(OpenApiOperation operation, XPathNodeIterator responseNodes)
        {
            while (responseNodes.MoveNext())
            {
                var code = responseNodes.Current.GetAttribute("code", "");
                var response = operation.Responses.ContainsKey(code)
                    ? operation.Responses[code]
                    : operation.Responses[code] = new OpenApiResponse();

                response.Description = XmlCommentsTextHelper.Humanize(responseNodes.Current.InnerXml);
            }
        }
    }

    internal class TribalXmlCommentsSchemaFilter : ISchemaFilter
    {
        private readonly Dictionary<string, XPathNavigator> _docMembers;

        public TribalXmlCommentsSchemaFilter(XPathDocument xmlDoc)
        {
            var xmlNavigator = xmlDoc.CreateNavigator();
            _docMembers = new Dictionary<string, XPathNavigator>();
            foreach (XPathNavigator memberNode in xmlDoc.CreateNavigator().Select("/doc/members/member"))
            {
                var memberName = memberNode.GetAttribute("name", "");
                _docMembers[memberName] = memberNode;
            }
        }

        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            ApplyTypeTags(schema, context.Type);

            if (context.MemberInfo != null)
            {
                ApplyMemberTags(schema, context);
            }
        }

        private void ApplyTypeTags(OpenApiSchema schema, Type type)
        {
            var typeMemberName = XmlCommentsNodeNameHelper.GetMemberNameForType(type);
            var typeSummaryNode = _docMembers.TryGetValue(typeMemberName, out var result) ? result.SelectSingleNode("summary") : null;

            if (typeSummaryNode != null)
            {
                schema.Description = XmlCommentsTextHelper.Humanize(typeSummaryNode.InnerXml);
            }
        }

        private void ApplyMemberTags(OpenApiSchema schema, SchemaFilterContext context)
        {
            var fieldOrPropertyMemberName = XmlCommentsNodeNameHelper.GetMemberNameForFieldOrProperty(context.MemberInfo);

            if (!_docMembers.TryGetValue(fieldOrPropertyMemberName, out var fieldOrPropertyNode)) return;

            var summaryNode = fieldOrPropertyNode.SelectSingleNode("summary");
            if (summaryNode != null)
                schema.Description = XmlCommentsTextHelper.Humanize(summaryNode.InnerXml);

            var exampleNode = fieldOrPropertyNode.SelectSingleNode("example");
            if (exampleNode != null)
            {
                var exampleAsJson = (schema.ResolveType(context.SchemaRepository) == "string") && !exampleNode.Value.Equals("null")
                    ? $"\"{exampleNode.ToString()}\""
                    : exampleNode.ToString();

                schema.Example = OpenApiAnyFactory.CreateFromJson(exampleAsJson);
            }
        }
    }
}
