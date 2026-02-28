using BiomePampa.ViewModels;

namespace BiomePampa.Views
{
    public partial class BatchListPage : ContentPage
    {
        public BatchListPage(BatchListViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            
            if (BindingContext is BatchListViewModel viewModel)
            {
                await viewModel.LoadBatchesCommand.ExecuteAsync(null);
            }
        }
    }
}
