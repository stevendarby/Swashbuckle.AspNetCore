using System;
using System.Xml.XPath;
using Microsoft.OpenApi.Models;

namespace Swashbuckle.AspNetCore.SwaggerGen
{
    public class XmlCommentsSchemaFilter : ISchemaFilter
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
