using BiomePampa.ViewModels;

namespace BiomePampa.Views
{
    public partial class CustomerDetailPage : ContentPage
    {
        public CustomerDetailPage(CustomerDetailViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            
            if (BindingContext is CustomerDetailViewModel viewModel)
            {
                await viewModel.LoadCustomerCommand.ExecuteAsync(null);
            }
        }
    }
}
