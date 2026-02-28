using BiomePampa.Infrastructure.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BiomePampa.ViewModels
{
    public partial class DatabaseInfoViewModel : BaseViewModel
    {
        private readonly ApplicationDbContext _context;

        [ObservableProperty]
        private string databaseInfo = "Carregando...";

        [ObservableProperty]
        private string databasePath = string.Empty;

        public DatabaseInfoViewModel(ApplicationDbContext context)
        {
            _context = context;
            Title = "Informações do Banco";
        }

        [RelayCommand]
        private async Task LoadInfoAsync()
        {
            try
            {
                IsBusy = true;
                DatabasePath = Helpers.DatabaseHelper.GetDatabasePath();
                DatabaseInfo = await Helpers.DatabaseHelper.GetDatabaseInfoAsync(_context);
            }
            catch (Exception ex)
            {
                DatabaseInfo = $"Erro ao carregar informações: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task CopyPathAsync()
        {
            await Clipboard.SetTextAsync(DatabasePath);
            await Shell.Current.DisplayAlert("Sucesso", "Caminho copiado para a área de transferência!", "OK");
        }

        [RelayCommand]
        private async Task InitializeDatabaseAsync()
        {
            try
            {
                IsBusy = true;
                var success = await Helpers.DatabaseHelper.InitializeDatabaseAsync(_context);
                if (success)
                {
                    await Shell.Current.DisplayAlert("Sucesso", "Banco de dados inicializado!", "OK");
                    await LoadInfoAsync();
                }
                else
                {
                    await Shell.Current.DisplayAlert("Erro", "Falha ao inicializar o banco de dados", "OK");
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Erro", ex.Message, "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task DeleteDatabaseAsync()
        {
            var confirm = await Shell.Current.DisplayAlert(
                "Confirmar",
                "Deseja realmente excluir o banco de dados? Todos os dados serão perdidos!",
                "Sim",
                "Não");

            if (!confirm)
                return;

            try
            {
                IsBusy = true;
                var success = await Helpers.DatabaseHelper.DeleteDatabaseAsync();
                if (success)
                {
                    await Shell.Current.DisplayAlert("Sucesso", "Banco de dados excluído!", "OK");
                    await LoadInfoAsync();
                }
                else
                {
                    await Shell.Current.DisplayAlert("Erro", "Falha ao excluir o banco de dados", "OK");
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Erro", ex.Message, "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task OpenInExplorerAsync()
        {
            try
            {
                var folder = Path.GetDirectoryName(DatabasePath);
                if (!string.IsNullOrEmpty(folder))
                {
                    await Launcher.OpenAsync(new OpenFileRequest
                    {
                        File = new ReadOnlyFile(folder)
                    });
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Erro", $"Não foi possível abrir a pasta: {ex.Message}", "OK");
            }
        }
    }
}
