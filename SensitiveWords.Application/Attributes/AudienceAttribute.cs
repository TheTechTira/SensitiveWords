namespace SensitiveWords.Application.Attributes
{
    /// <summary>
    /// Audience marker for classes or methods (e.g., "internal" vs. "external").
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class AudienceAttribute : Attribute
    {
        public const string Internal = "internal";
        public const string External = "external";
        public AudienceAttribute(string value) => Value = value;
        public string Value { get; }
    }
}
