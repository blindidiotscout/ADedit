namespace HieUserEditor.Models
{
    /// <summary>
    /// Repräsentiert eine Active Directory Gruppe.
    /// </summary>
    public class AdGroup
    {
        public string Name { get; set; } = string.Empty;
        public string DistinguishedName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public override string ToString() => Name;
    }
}
