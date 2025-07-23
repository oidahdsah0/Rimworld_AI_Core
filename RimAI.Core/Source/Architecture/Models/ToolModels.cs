using System.Collections.Generic;

namespace RimAI.Core.Architecture.Models
{
    /// <summary>
    /// Represents a tool that can be called by the AI, conforming to the OpenAI 'function' tool type.
    /// </summary>
    public class AITool
    {
        public string Type { get; set; } = "function";
        public AIFunction Function { get; set; }
    }

    /// <summary>
    /// Describes the function signature for an AI tool.
    /// </summary>
    public class AIFunction
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public AIParameterSchema Parameters { get; set; }
    }

    /// <summary>
    /// Defines the JSON schema for the parameters of a function.
    /// </summary>
    public class AIParameterSchema
    {
        public string Type { get; set; } = "object";
        public Dictionary<string, AIParameterProperty> Properties { get; set; }
        public List<string> Required { get; set; }

        public AIParameterSchema()
        {
            Properties = new Dictionary<string, AIParameterProperty>();
            Required = new List<string>();
        }
    }

    /// <summary>
    /// Describes a single property within the parameter schema of a function.
    /// </summary>
    public class AIParameterProperty
    {
        /// <summary>
        /// The data type of the parameter (e.g., "string", "integer", "boolean").
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// A description of what the parameter is used for.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Optional. A list of acceptable values for the parameter.
        /// </summary>
        public List<string> Enum { get; set; }
    }
} 