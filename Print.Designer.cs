namespace PrintApp
{
    partial class Print
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            SuspendLayout();
            // 
            // Print
            // 
            ClientSize = new Size(284, 261);
            Name = "Print";
            Text = "Print";
            ResumeLayout(false);
        }
    }
}