using BiomePampa.Application.DTOs.Batches;
using BiomePampa.Application.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BiomePampa.ViewModels
{
    [QueryProperty(nameof(BatchId), "id")]
    public partial class BatchDetailViewModel : BaseViewModel
    {
        private readonly IBatchService _batchService;

        [ObservableProperty]
        private Guid? batchId;

        [ObservableProperty]
        private string batchNumber = string.Empty;

        [ObservableProperty]
        private Guid productId;

        [ObservableProperty]
        private DateTime productionDate = DateTime.Today;

        [ObservableProperty]
        private DateTime expirationDate = DateTime.Today.AddMonths(6);

        [ObservableProperty]
        private decimal initialQuantity;

        [ObservableProperty]
        private decimal currentQuantity;

        [ObservableProperty]
        private Guid? supplierId;

        [ObservableProperty]
        private string notes = string.Empty;

        public BatchDetailViewModel(IBatchService batchService)
        {
            _batchService = batchService;
        }

        [RelayCommand]
        private async Task LoadBatchAsync()
        {
            if (BatchId == null || BatchId == Guid.Empty)
            {
                Title = "Novo Lote";
                return;
            }

            try
            {
                IsBusy = true;
                Title = "Editar Lote";

                var batch = await _batchService.GetByIdAsync(BatchId.Value);
                if (batch != null)
                {
                    BatchNumber = batch.BatchNumber;
                    ProductId = batch.ProductId;
                    ProductionDate = batch.ProductionDate;
                    ExpirationDate = batch.ExpirationDate;
                    InitialQuantity = batch.InitialQuantity;
                    CurrentQuantity = batch.CurrentQuantity;
                    SupplierId = batch.SupplierId;
                    Notes = batch.Notes ?? string.Empty;
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Erro", $"Não foi possível carregar o lote: {ex.Message}", "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            if (IsBusy)
                return;

            if (string.IsNullOrWhiteSpace(BatchNumber))
            {
                await Shell.Current.DisplayAlert("Validação", "Número do lote é obrigatório", "OK");
                return;
            }

            if (ProductId == Guid.Empty)
            {
                await Shell.Current.DisplayAlert("Validação", "Selecione um produto", "OK");
                return;
            }

            try
            {
                IsBusy = true;

                if (BatchId == null || BatchId == Guid.Empty)
                {
                    var createDto = new CreateBatchDto(
                        BatchNumber,
                        ProductId,
                        ProductionDate,
                        ExpirationDate,
                        InitialQuantity,
                        SupplierId,
                        Notes
                    );

                    await _batchService.CreateAsync(createDto);
                    await Shell.Current.DisplayAlert("Sucesso", "Lote criado com sucesso!", "OK");
                }
                else
                {
                    var updateDto = new UpdateBatchDto(
                        BatchNumber,
                        ProductionDate,
                        ExpirationDate,
                        CurrentQuantity,
                        SupplierId,
                        Notes
                    );

                    await _batchService.UpdateAsync(BatchId.Value, updateDto);
                    await Shell.Current.DisplayAlert("Sucesso", "Lote atualizado com sucesso!", "OK");
                }

                await Shell.Current.GoToAsync("..");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Erro", $"Não foi possível salvar o lote: {ex.Message}", "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task CancelAsync()
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}
