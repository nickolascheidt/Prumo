using BiomePampa.Views;

namespace BiomePampa
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // Register routes for navigation
            Routing.RegisterRoute("productdetail", typeof(ProductDetailPage));
            Routing.RegisterRoute("batchdetail", typeof(BatchDetailPage));
            Routing.RegisterRoute("customerdetail", typeof(CustomerDetailPage));
        }
    }
}
