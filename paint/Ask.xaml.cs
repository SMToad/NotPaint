using System.Windows;
namespace paint
{
   public partial class Ask : Window
    {
        public Ask()
        {
            InitializeComponent();
        }
        public bool choice;
        public void Accept_Click(object sender,RoutedEventArgs e)
        {
            choice = true;
            this.DialogResult = true;
        }
        public void Deny_Click(object sender, RoutedEventArgs e)
        {
            choice = false;
            this.DialogResult = true;
        }
    }
}
