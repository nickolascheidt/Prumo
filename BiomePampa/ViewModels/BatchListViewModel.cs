using BiomePampa.Application.DTOs.Batches;
using BiomePampa.Application.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace BiomePampa.ViewModels
{
    public partial class BatchListViewModel : BaseViewModel
    {
        private readonly IBatchService _batchService;

        [ObservableProperty]
        private ObservableCollection<BatchDto> batches = new();

        [ObservableProperty]
        private BatchDto? selectedBatch;

        public BatchListViewModel(IBatchService batchService)
        {
            _batchService = batchService;
            Title = "Lotes";
        }

        [RelayCommand]
        private async Task LoadBatchesAsync()
        {
            if (IsBusy)
                return;

            try
            {
                IsBusy = true;
                var items = await _batchService.GetAllAsync();
                Batches.Clear();
                foreach (var item in items)
                {
                    Batches.Add(item);
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Erro", $"Não foi possível carregar os lotes: {ex.Message}", "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task AddBatchAsync()
        {
            await Shell.Current.GoToAsync("batchdetail");
        }

        [RelayCommand]
        private async Task EditBatchAsync(BatchDto batch)
        {
            if (batch == null)
                return;

            await Shell.Current.GoToAsync($"batchdetail?id={batch.Id}");
        }

        [RelayCommand]
        private async Task DeleteBatchAsync(BatchDto batch)
        {
            if (batch == null)
                return;

            bool confirm = await Shell.Current.DisplayAlert(
                "Confirmar",
                $"Deseja realmente excluir o lote '{batch.BatchNumber}'?",
                "Sim",
                "Não");

            if (!confirm)
                return;

            try
            {
                IsBusy = true;
                await _batchService.DeleteAsync(batch.Id);
                Batches.Remove(batch);
                await Shell.Current.DisplayAlert("Sucesso", "Lote excluído com sucesso!", "OK");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Erro", $"Não foi possível excluir o lote: {ex.Message}", "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            await LoadBatchesAsync();
        }
    }
}
