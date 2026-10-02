using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
//using System.Reflection.Emit;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text;
//using static System.Net.Mime.MediaTypeNames;
using System.Windows.Forms;
//using System.Data.SQLite;



namespace VulnerableWinForm
{
    internal class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // .NET Framework 4.8 classic WinForms startup
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
            System.Windows.Forms.Application.Run(new MainForm());

        }
    }
}
