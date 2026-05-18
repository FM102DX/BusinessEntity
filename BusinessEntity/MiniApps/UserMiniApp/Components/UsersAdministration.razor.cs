using BusinessEntity.MiniApps.UserMiniApp.Contracts.Connectors;
using BusinessEntity.MiniApps.UserMiniApp.Contracts.Dtos;
using Microsoft.AspNetCore.Components;

namespace BusinessEntity.MiniApps.UserMiniApp.Components
{
    // Компонент административного CRUD локальных пользователей UserMiniApp.
    public partial class UsersAdministration : ComponentBase
    {
        [Inject] public IUserConnector UserConnector { get; set; } = default!;

        private IReadOnlyList<UserAdministrationRecord> Users { get; set; } = Array.Empty<UserAdministrationRecord>();
        private UserAdministrationSaveRequest EditModel { get; set; } = new();
        private Guid? SelectedUserId { get; set; }
        private Guid? DeleteConfirmationUserId { get; set; }
        private bool IsLoading { get; set; }
        private bool IsCreating { get; set; }
        private bool IsSaving { get; set; }
        private bool IsDeleting { get; set; }
        private string? StatusMessage { get; set; }
        private string? ErrorMessage { get; set; }
        private UserAdministrationTab ActiveTab { get; set; } = UserAdministrationTab.General;
        private bool IsBusy => IsLoading || IsCreating || IsSaving || IsDeleting;

        // Загружает пользователей при первом открытии компонента.
        protected override async Task OnInitializedAsync()
        {
            await LoadUsersAsync();
        }

        // Загружает список пользователей без принудительного выбора конкретной записи.
        private Task LoadUsersAsync()
        {
            return LoadUsersCoreAsync(null);
        }

        // Загружает список локальных пользователей и выбирает нужную запись.
        private async Task LoadUsersCoreAsync(Guid? userIdToSelect)
        {
            IsLoading = true;
            ErrorMessage = null;

            try
            {
                Users = await UserConnector.GetAdministrationUsersAsync();
                var selectedId = userIdToSelect ?? SelectedUserId;
                var selectedUser = selectedId.HasValue
                    ? Users.FirstOrDefault(user => user.Id == selectedId.Value)
                    : Users.FirstOrDefault();

                if (selectedUser == null)
                {
                    ClearSelection();
                }
                else
                {
                    SelectUser(selectedUser);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }

        // Создает нового Authentik-пользователя приложения и выбирает его в списке.
        private async Task CreateUserAsync()
        {
            IsCreating = true;
            StatusMessage = null;
            ErrorMessage = null;

            try
            {
                var createdUser = await UserConnector.CreateAdministrationUserAsync();
                await LoadUsersCoreAsync(createdUser.Id);
                StatusMessage = $"Пользователь {createdUser.AuthentikLogin} создан в Authentik.";
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsCreating = false;
            }
        }

        // Выбирает пользователя из списка и переносит его данные в форму справа.
        private void SelectUser(UserAdministrationRecord user)
        {
            SelectedUserId = user.Id;
            DeleteConfirmationUserId = null;
            StatusMessage = null;
            ErrorMessage = null;
            EditModel = new UserAdministrationSaveRequest
            {
                ExternalId = user.ExternalId,
                AuthentikLogin = user.AuthentikLogin,
                DisplayedName = user.DisplayedName
            };
        }

        // Обновляет Authentik-логин и локальное отображаемое имя через UserMiniApp connector.
        private async Task SaveUserAsync()
        {
            if (!SelectedUserId.HasValue)
            {
                return;
            }

            IsSaving = true;
            ErrorMessage = null;
            StatusMessage = null;

            try
            {
                var savedUser = await UserConnector.UpdateAdministrationUserAsync(SelectedUserId.Value, EditModel);

                await LoadUsersCoreAsync(savedUser.Id);
                StatusMessage = "Пользователь сохранен.";
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsSaving = false;
            }
        }

        // Включает второй шаг подтверждения удаления выбранного пользователя.
        private void RequestDeleteConfirmation()
        {
            DeleteConfirmationUserId = SelectedUserId;
            StatusMessage = "Подтвердите удаление пользователя.";
            ErrorMessage = null;
        }

        // Отменяет подтверждение удаления выбранного пользователя.
        private void CancelDeleteConfirmation()
        {
            DeleteConfirmationUserId = null;
            StatusMessage = null;
        }

        // Удаляет выбранного локального пользователя и его технические свойства.
        private async Task DeleteSelectedUserAsync()
        {
            if (!SelectedUserId.HasValue)
            {
                return;
            }

            IsDeleting = true;
            ErrorMessage = null;
            StatusMessage = null;

            try
            {
                var deleted = await UserConnector.DeleteAdministrationUserAsync(SelectedUserId.Value);
                var statusMessage = deleted ? "Пользователь удален." : "Пользователь не найден.";
                SelectedUserId = null;
                DeleteConfirmationUserId = null;
                await LoadUsersAsync();
                StatusMessage = statusMessage;
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsDeleting = false;
            }
        }

        // Переключает вкладку формы выбранного пользователя.
        private void SetActiveTab(UserAdministrationTab tab)
        {
            ActiveTab = tab;
        }

        // Возвращает CSS-класс кнопки пользователя в левом списке.
        private string GetUserButtonClass(UserAdministrationRecord user)
        {
            return SelectedUserId == user.Id
                ? "users-admin__user users-admin__user--selected"
                : "users-admin__user";
        }

        // Возвращает CSS-класс кнопки вкладки формы пользователя.
        private string GetTabButtonClass(UserAdministrationTab tab)
        {
            return ActiveTab == tab
                ? "users-admin__tab users-admin__tab--active"
                : "users-admin__tab";
        }

        // Возвращает отображаемое имя для строки пользователя в списке.
        private static string GetUserListName(UserAdministrationRecord user)
        {
            return string.IsNullOrWhiteSpace(user.DisplayedName)
                ? user.AuthentikLogin
                : user.DisplayedName;
        }

        // Очищает форму, когда в Authentik нет пользователей приложения.
        private void ClearSelection()
        {
            SelectedUserId = null;
            DeleteConfirmationUserId = null;
            ActiveTab = UserAdministrationTab.General;
            EditModel = new UserAdministrationSaveRequest();
        }

        // Вкладки формы локального пользователя.
        private enum UserAdministrationTab
        {
            General,
            AccessRights
        }
    }
}
