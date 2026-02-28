using BiomePampa.ViewModels;

namespace BiomePampa.Views
{
    public partial class CustomerListPage : ContentPage
    {
        public CustomerListPage(CustomerListViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            
            if (BindingContext is CustomerListViewModel viewModel)
            {
                await viewModel.LoadCustomersCommand.ExecuteAsync(null);
            }
        }
    }
}
