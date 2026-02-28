using BiomePampa.ViewModels;

namespace BiomePampa.Views
{
    public partial class ProductDetailPage : ContentPage
    {
        public ProductDetailPage(ProductDetailViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            
            if (BindingContext is ProductDetailViewModel viewModel)
            {
                await viewModel.LoadProductCommand.ExecuteAsync(null);
            }
        }
    }
}
