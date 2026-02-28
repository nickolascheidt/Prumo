using BiomePampa.ViewModels;

namespace BiomePampa.Views
{
    public partial class DatabaseInfoPage : ContentPage
    {
        public DatabaseInfoPage(DatabaseInfoViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            
            if (BindingContext is DatabaseInfoViewModel viewModel)
            {
                await viewModel.LoadInfoCommand.ExecuteAsync(null);
            }
        }
    }
}
