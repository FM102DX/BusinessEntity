using System.Collections.ObjectModel;
using BusinessEntity.Contracts;
using BusinessEntity.Core.Contracts;
using BusinessEntity.Core.Classes;
using BusinessEntity.Data;
using BusinessEntity.Data.Messages;
using BusinessEntity.Data.Services;
using BusinessEntity.Services;
using DynamicData;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using ReactiveUI;
using SampleOnlineMall.WebLogger.Models;
using System.Security.Claims;

namespace BusinessEntity.Pages
{    public partial class Index : ComponentBase
    {
        [Inject] public IApplicationSideAuthService AuthService { get; set; } = default!;
        [Inject] public NavigationManager Navigation { get; set; } = default!;
        [Inject] public ILogger<Index> Logger { get; set; } = default!;
        [Inject] public IPossibleEntityRelationTypesProvider RelationTypesProvider { get; set; } = default!;
        
        private string? CurrentUserName { get; set; }
        private string? CurrentUserId { get; set; }
        private bool IsAuthenticated { get; set; }
        private IEnumerable<MacroRelationType> PossibleRelations { get; set; } = new List<MacroRelationType>();
        private IEnumerable<string> EntityTypeEnums { get; set; } = new List<string>();
        private IEnumerable<string> RelationTypeEnums { get; set; } = new List<string>();

        protected override async Task OnInitializedAsync()
        {
            try
            {
                IsAuthenticated = await AuthService.IsUserAuthenticatedAsync();
                  if (IsAuthenticated)
                {
                    CurrentUserName = await AuthService.GetUserNameAsync();
                    
                    // Получаем ID пользователя из клеймов
                    var currentUser = await AuthService.GetCurrentUserAsync();
                    if (currentUser?.Identity != null)
                    {
                        CurrentUserId = currentUser.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                                       ?? currentUser.FindFirst("sub")?.Value;
                    }
                    
                    // Получаем возможные отношения между сущностями
                    PossibleRelations = RelationTypesProvider.GetPossibleRelations();
                    
                    // Получаем все возможные типы сущностей из enum
                    EntityTypeEnums = Enum.GetNames(typeof(BusinessEntityTypeEnum));
                    
                    // Получаем все возможные типы отношений из enum
                    RelationTypeEnums = Enum.GetNames(typeof(BusinessEntityRelationTypeEnum));
                    
                    Logger.LogInformation($"User {CurrentUserName} accessed main page");
                }
                else
                {
                    Logger.LogInformation("Anonymous user accessed main page");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error loading user information on main page");
            }
        }

        private void RedirectToLogin()
        {
            try
            {
                var loginUrl = AuthService.GetLoginUrl("/");
                Logger.LogInformation($"Redirecting user to Authentic login url={loginUrl}");
                Navigation.NavigateTo(loginUrl, forceLoad: true);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error redirecting to login");
            }
        }
    }
}
