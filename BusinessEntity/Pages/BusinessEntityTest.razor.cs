using Microsoft.AspNetCore.Components;
using BusinessEntity.Core.Services;
using BusinessEntity.Core.Classes;
using BusinessEntity.Core.Contracts;
using BusinessEntityClass = BusinessEntity.Core.Classes.BusinessEntity;

namespace BusinessEntity.Pages
{
    public partial class BusinessEntityTest : ComponentBase
    {
        [Inject] public BusinessEntityHelper BusinessEntityHelper { get; set; } = default!;
        [Inject] public IPossibleEntityRelationTypesProvider RelationTypesProvider { get; set; } = default!;
        [Inject] public ISampleDataService SampleDataService { get; set; } = default!;

        private string newEntityName = "";
        private BusinessEntityTypeEnum selectedEntityType = BusinessEntityTypeEnum.Space;
        private List<BusinessEntityClass> entities = new();
        private List<Relation> relations = new();
        private List<MacroRelationType> relationTypes = new();
        
        private string selectedEntityAId = "";
        private string selectedEntityBId = "";
        private string selectedRelationType = "";
        private string relationParameters = "";
        
        private string message = "";

        protected override async Task OnInitializedAsync()
        {
            // Инициализируем демо-данные
            await SampleDataService.InitializeSampleDataAsync();
            
            await LoadEntities();
            await LoadRelations();
            LoadRelationTypes();
        }

        private async Task CreateEntity()
        {
            if (string.IsNullOrWhiteSpace(newEntityName))
            {
                message = "Please enter entity name";
                return;
            }

            try
            {
                var entity = await BusinessEntityHelper.CreateBusinessEntity(selectedEntityType, newEntityName);
                message = $"Created entity: {entity.Name} ({entity.EntityType})";
                newEntityName = "";
                await LoadEntities();
            }
            catch (Exception ex)
            {
                message = $"Error creating entity: {ex.Message}";
            }
        }

        private async Task DeleteEntity(Guid id)
        {
            try
            {
                await BusinessEntityHelper.RemoveBusinessEntity(id);
                message = "Entity deleted successfully";
                await LoadEntities();
                await LoadRelations();
            }
            catch (Exception ex)
            {
                message = $"Error deleting entity: {ex.Message}";
            }
        }

        private async Task CreateRelation()
        {
            if (string.IsNullOrEmpty(selectedEntityAId) || string.IsNullOrEmpty(selectedEntityBId) || string.IsNullOrEmpty(selectedRelationType))
            {
                message = "Please select both entities and relation type";
                return;
            }

            try
            {
                var entityA = entities.FirstOrDefault(e => e.Id.ToString() == selectedEntityAId);
                var entityB = entities.FirstOrDefault(e => e.Id.ToString() == selectedEntityBId);
                var relationType = relationTypes.FirstOrDefault(r => r.RelationName == selectedRelationType);

                if (entityA == null || entityB == null || relationType == null)
                {
                    message = "Invalid selection";
                    return;
                }

                var relation = await BusinessEntityHelper.CreateRelation(entityA, entityB, relationType, relationParameters);
                message = $"Created relation: {relation.RelationType}";
                
                selectedEntityAId = "";
                selectedEntityBId = "";
                selectedRelationType = "";
                relationParameters = "";
                
                await LoadRelations();
            }
            catch (Exception ex)
            {
                message = $"Error creating relation: {ex.Message}";
            }
        }

        private async Task LoadEntities()
        {
            try
            {
                var result = await BusinessEntityHelper.GetAllBusinessEntities();
                entities = result.ToList();
            }
            catch (Exception ex)
            {
                message = $"Error loading entities: {ex.Message}";
            }
        }

        private async Task LoadRelations()
        {
            try
            {
                var result = await BusinessEntityHelper.GetAllRelations();
                relations = result.ToList();
            }
            catch (Exception ex)
            {
                message = $"Error loading relations: {ex.Message}";
            }
        }

        private void LoadRelationTypes()
        {
            relationTypes = RelationTypesProvider.GetPossibleRelations().ToList();
        }

        private string GetEntityName(Guid entityId)
        {
            var entity = entities.FirstOrDefault(e => e.Id == entityId);
            return entity?.Name ?? "Unknown";
        }
    }
} 