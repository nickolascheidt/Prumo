using BiomePampa.ViewModels;

namespace BiomePampa.Views
{
    public partial class ProductListPage : ContentPage
    {
        public ProductListPage(ProductListViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            
            if (BindingContext is ProductListViewModel viewModel)
            {
                await viewModel.LoadProductsCommand.ExecuteAsync(null);
            }
        }
    }
}
