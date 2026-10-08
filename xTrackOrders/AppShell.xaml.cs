using xTrackOrders.Views;

namespace xTrackOrders
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute(nameof(OrderPage), typeof(OrderPage));
        }
    }
}
