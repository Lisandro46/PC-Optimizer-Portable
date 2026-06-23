using System.Windows.Controls;

namespace PcOptimizer
{
    public partial class PlaceholderView : UserControl
    {
        public PlaceholderView(string title, string subtitle)
        {
            InitializeComponent();
            TitleText.Text = title;
            SubText.Text = subtitle;
        }
    }
}
