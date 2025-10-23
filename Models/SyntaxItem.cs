namespace SNIBypassGUI.Models
{
    public class SyntaxItem(string syntax, string description)
    {
        public string Syntax { get; } = syntax;
        public string Description { get; } = description;
    }
}
