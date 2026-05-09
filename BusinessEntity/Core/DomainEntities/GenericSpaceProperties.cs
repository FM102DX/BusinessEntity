namespace BusinessEntity.Core.DomainEntities
{
    // Общие настройки пространства, хранящиеся в BusinessEntityProperties.
    public sealed class GenericSpaceProperties
    {
        public int SchemaVersion { get; set; } = 1;

        public string Kind { get; set; } = nameof(GenericSpaceProperties);

        public bool DoBackup { get; set; }
    }
}
