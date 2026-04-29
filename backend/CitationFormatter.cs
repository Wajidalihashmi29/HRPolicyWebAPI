using System;
using System.Text.Json;
using Azure.AI.Extensions.OpenAI;
using OpenAI.Responses;

#pragma warning disable OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

namespace VirtualHRFoundryAgent
{
    /// <summary>
    /// Custom citation formatter function tool that processes file search citations
    /// and creates formatted citation sections with document link, text, page, section, and chunk properties.
    /// </summary>
    public static class CitationFormatter
    {
        /// <summary>
        /// The function tool definition for citation formatting.
        /// </summary>
        public static readonly FunctionTool FormatCitationTool = ResponseTool.CreateFunctionTool(
            functionName: "formatCitation",
            functionDescription: "Formats a citation from file search results into a structured citation section with document link, text, page, section, and chunk properties.",
            functionParameters: BinaryData.FromString("{\n" +
                "  \"type\": \"object\",\n" +
                "  \"properties\": {\n" +
                "    \"documentId\": {\n" +
                "      \"type\": \"string\",\n" +
                "      \"description\": \"The unique identifier of the document (file ID from the file search results).\"\n" +
                "    },\n" +
                "    \"documentName\": {\n" +
                "      \"type\": \"string\",\n" +
                "      \"description\": \"The name of the document file.\"\n" +
                "    },\n" +
                "    \"chunkText\": {\n" +
                "      \"type\": \"string\",\n" +
                "      \"description\": \"The text chunk or excerpt from the document that supports the response.\"\n" +
                "    },\n" +
                "    \"pageNumber\": {\n" +
                "      \"type\": [\"integer\", \"null\"],\n" +
                "      \"description\": \"The page number where the citation text is found (if available).\"\n" +
                "    },\n" +
                "    \"section\": {\n" +
                "      \"type\": [\"string\", \"null\"],\n" +
                "      \"description\": \"The section or chapter name where the citation text is found (if available).\"\n" +
                "    }\n" +
                "  },\n" +
                "  \"required\": [\"documentId\", \"documentName\", \"chunkText\", \"pageNumber\", \"section\"],\n" +
                "  \"additionalProperties\": false\n" +
                "}"),
            strictModeEnabled: true
        );

        /// <summary>
        /// Formats a citation with all available properties into a structured string.
        /// </summary>
        /// <param name="documentId">The document/file ID.</param>
        /// <param name="documentName">The document filename.</param>
        /// <param name="chunkText">The citation text/chunk.</param>
        /// <param name="pageNumber">Optional page number.</param>
        /// <param name="section">Optional section name.</param>
        /// <returns>A formatted citation string with all properties.</returns>
        public static string FormatCitation(string documentId, string documentName, string chunkText, int? pageNumber = null, string? section = null)
        {
            var citation = new
            {
                DocumentLink = $"file://{documentName}",
                DocumentId = documentId,
                DocumentName = documentName,
                Text = chunkText,
                Page = pageNumber,
                Section = section,
                Chunk = chunkText
            };

            // Format as a structured citation section
            string formattedCitation = $"\n---CITATION---\n" +
                $"Document: {citation.DocumentName}\n" +
                $"ID: {citation.DocumentId}\n";

            if (!string.IsNullOrEmpty(citation.Section))
            {
                formattedCitation += $"Section: {citation.Section}\n";
            }

            if (citation.Page.HasValue)
            {
                formattedCitation += $"Page: {citation.Page.Value}\n";
            }

            formattedCitation += $"Text: {citation.Text}\n" +
                $"---END CITATION---\n";

            return formattedCitation;
        }

        /// <summary>
        /// Executes the citation formatter function based on the function call arguments.
        /// </summary>
        /// <param name="item">The function call response item containing the arguments.</param>
        /// <returns>A function call output response item with the formatted citation.</returns>
        public static FunctionCallOutputResponseItem GetResolvedToolOutput(FunctionCallResponseItem item)
        {   
            if (item.FunctionName != FormatCitationTool.FunctionName)
            {
                Console.WriteLine($"[CitationFormatter] Not formatCitation, returning null");
                return null;
            }

            using JsonDocument argumentsJson = JsonDocument.Parse(item.FunctionArguments);
            var root = argumentsJson.RootElement;

            // Extract required parameters
            string documentId = root.GetProperty("documentId").GetString()!;
            string documentName = root.GetProperty("documentName").GetString()!;
            string chunkText = root.GetProperty("chunkText").GetString()!;

            // Extract optional parameters
            int? pageNumber = null;
            if (root.TryGetProperty("pageNumber", out JsonElement pageElement))
            {
                if (pageElement.ValueKind == JsonValueKind.Number && pageElement.TryGetInt32(out var pageValue))
                {
                    pageNumber = pageValue;
                }
                else if (pageElement.ValueKind == JsonValueKind.String &&
                         int.TryParse(pageElement.GetString() ?? string.Empty, out var pageValueFromString))
                {
                    pageNumber = pageValueFromString;
                }
            }

            string section = null;
            if (root.TryGetProperty("section", out JsonElement sectionElement))
            {
                section = sectionElement.ValueKind == JsonValueKind.String
                    ? sectionElement.GetString() ?? string.Empty
                    : sectionElement.ToString();
            }

            // Format and return the citation
            string formattedCitation = FormatCitation(documentId, documentName, chunkText, pageNumber, section);
            
            var result = ResponseItem.CreateFunctionCallOutputItem(item.CallId, formattedCitation);
            return result;
        }
    }
}