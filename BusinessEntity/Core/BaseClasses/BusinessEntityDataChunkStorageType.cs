namespace BusinessEntity.Core.Classes;

// Описывает способ chunk-хранения payload-части бизнес-сущности.
public enum BusinessEntityDataChunkStorageType
{
    // Payload не использует chunk-хранение.
    None = 0,
    // Payload хранится текстовыми чанками.
    TextChunks = 1,
    // Payload хранится бинарными чанками.
    ByteChunks = 2
}
