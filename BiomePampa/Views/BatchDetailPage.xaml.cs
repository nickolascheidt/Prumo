using BiomePampa.ViewModels;

namespace BiomePampa.Views
{
    public partial class BatchDetailPage : ContentPage
    {
        public BatchDetailPage(BatchDetailViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            
            if (BindingContext is BatchDetailViewModel viewModel)
            {
                await viewModel.LoadBatchCommand.ExecuteAsync(null);
            }
        }
    }
}
