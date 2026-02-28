using BiomePampa.Application.DTOs.Products;
using BiomePampa.Application.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace BiomePampa.ViewModels
{
    public partial class ProductListViewModel : BaseViewModel
    {
        private readonly IProductService _productService;

        [ObservableProperty]
        private ObservableCollection<ProductDto> products = new();

        [ObservableProperty]
        private ProductDto? selectedProduct;

        public ProductListViewModel(IProductService productService)
        {
            _productService = productService;
            Title = "Produtos";
        }

        [RelayCommand]
        private async Task LoadProductsAsync()
        {
            if (IsBusy)
                return;

            try
            {
                IsBusy = true;
                var items = await _productService.GetAllAsync();
                Products.Clear();
                foreach (var item in items)
                {
                    Products.Add(item);
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Erro", $"Não foi possível carregar os produtos: {ex.Message}", "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task AddProductAsync()
        {
            await Shell.Current.GoToAsync("productdetail");
        }

        [RelayCommand]
        private async Task EditProductAsync(ProductDto product)
        {
            if (product == null)
                return;

            await Shell.Current.GoToAsync($"productdetail?id={product.Id}");
        }

        [RelayCommand]
        private async Task DeleteProductAsync(ProductDto product)
        {
            if (product == null)
                return;

            bool confirm = await Shell.Current.DisplayAlert(
                "Confirmar",
                $"Deseja realmente excluir o produto '{product.Name}'?",
                "Sim",
                "Não");

            if (!confirm)
                return;

            try
            {
                IsBusy = true;
                await _productService.DeleteAsync(product.Id);
                Products.Remove(product);
                await Shell.Current.DisplayAlert("Sucesso", "Produto excluído com sucesso!", "OK");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Erro", $"Não foi possível excluir o produto: {ex.Message}", "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            await LoadProductsAsync();
        }
    }
}
