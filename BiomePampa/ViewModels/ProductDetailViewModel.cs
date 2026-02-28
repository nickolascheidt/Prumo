using BiomePampa.Application.DTOs.Products;
using BiomePampa.Application.Services;
using BiomePampa.Domain.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BiomePampa.ViewModels
{
    [QueryProperty(nameof(ProductIdParam), "id")]
    public partial class ProductDetailViewModel : BaseViewModel
    {
        private readonly IProductService _productService;

        [ObservableProperty]
        private string productIdParam = string.Empty;

        [ObservableProperty]
        private string productName = string.Empty;

        [ObservableProperty]
        private string productDescription = string.Empty;

        [ObservableProperty]
        private string productSKU = string.Empty;

        [ObservableProperty]
        private OliveOilType productOliveOilType = OliveOilType.ExtraVirgin;

        [ObservableProperty]
        private decimal productVolume;

        [ObservableProperty]
        private string productBarcode = string.Empty;

        [ObservableProperty]
        private decimal productMinimumStock;

        [ObservableProperty]
        private decimal productMaximumStock;

        [ObservableProperty]
        private decimal productUnitPrice;

        public ProductDetailViewModel(IProductService productService)
        {
            _productService = productService;
        }

        [RelayCommand]
        private async Task LoadProductAsync()
        {
            if (string.IsNullOrEmpty(ProductIdParam) || !Guid.TryParse(ProductIdParam, out var productId))
            {
                Title = "Novo Produto";
                return;
            }

            try
            {
                IsBusy = true;
                Title = "Editar Produto";

                var product = await _productService.GetByIdAsync(productId);
                if (product != null)
                {
                    ProductName = product.Name;
                    ProductDescription = product.Description;
                    ProductSKU = product.SKU;
                    ProductOliveOilType = product.OliveOilType;
                    ProductVolume = product.Volume;
                    ProductBarcode = product.Barcode ?? string.Empty;
                    ProductMinimumStock = product.MinimumStock;
                    ProductMaximumStock = product.MaximumStock;
                    ProductUnitPrice = product.UnitPrice;
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Erro", $"Não foi possível carregar o produto: {ex.Message}", "OK");
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

            if (string.IsNullOrWhiteSpace(ProductName))
            {
                await Shell.Current.DisplayAlert("Validação", "Nome é obrigatório", "OK");
                return;
            }

            try
            {
                IsBusy = true;

                if (string.IsNullOrEmpty(ProductIdParam) || !Guid.TryParse(ProductIdParam, out var productId))
                {
                    var createDto = new CreateProductDto(
                        ProductName,
                        ProductDescription,
                        ProductSKU,
                        ProductOliveOilType,
                        ProductVolume,
                        string.IsNullOrWhiteSpace(ProductBarcode) ? null : ProductBarcode,
                        ProductMinimumStock,
                        ProductMaximumStock,
                        ProductUnitPrice
                    );

                    await _productService.CreateAsync(createDto);
                    await Shell.Current.DisplayAlert("Sucesso", "Produto criado com sucesso!", "OK");
                }
                else
                {
                    var updateDto = new UpdateProductDto(
                        ProductName,
                        ProductDescription,
                        ProductSKU,
                        ProductOliveOilType,
                        ProductVolume,
                        string.IsNullOrWhiteSpace(ProductBarcode) ? null : ProductBarcode,
                        ProductMinimumStock,
                        ProductMaximumStock,
                        ProductUnitPrice
                    );

                    await _productService.UpdateAsync(productId, updateDto);
                    await Shell.Current.DisplayAlert("Sucesso", "Produto atualizado com sucesso!", "OK");
                }

                await Shell.Current.GoToAsync("..");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Erro", $"Não foi possível salvar o produto: {ex.Message}", "OK");
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
