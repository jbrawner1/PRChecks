using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;
using System.Windows.Forms;

namespace VulnerableWinForm
{
    public class MainForm : Form
    {
        private TabControl tabs;
        private TabPage tabLogin, tabNotes, /*tabDb*,*/ tabCmd, tabDeserialize, tabUpdate/*,tabAbout*/;

        // Insecure hard-coded credentials (vulnerability: hardcoded creds)
        private const string HardcodedUser = "admin";
        private const string HardcodedPass = "P@ssw0rd123"; // stored in source as plain text

        // Files used by the app
        private readonly string notesFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "notes.txt");
        private readonly string configFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.config.txt");
        private readonly string dbFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sample.db");

        public MainForm()
        {
            Text = "Vulnerable WinForms Sample (.NET 4.8)";
            Width = 900;
            Height = 600;

            tabs = new TabControl() { Dock = DockStyle.Fill };
            tabLogin = new TabPage("Login");
            tabNotes = new TabPage("Notes (Plaintext)");
            tabCmd = new TabPage("Run Command (Injection)");
            tabDeserialize = new TabPage("Deserialize (Unsafe)");
            tabUpdate = new TabPage("Updater (Insecure)");
            //tabAbout = new TabPage("About / Config");

            tabs.TabPages.AddRange(new[] { tabLogin, tabNotes, /*tabDb,*/ tabCmd, tabDeserialize, tabUpdate, /*tabAbout*/ });

            SetupLoginTab();
            SetupNotesTab();
            SetupCmdTab();
            SetupDeserializeTab();
            SetupUpdateTab();
            //SetupAboutTab();

            Controls.Add(tabs);

            // Initialize sample DB (insecurely using SQLite file in app folder with weak permissions)
            // Write sample insecure config
            File.WriteAllText(configFile, "api_key=1234567890abcdef\r\nupdate_url=http://insecure-updates.example.com/app.exe\r\n");
        }

        #region Login Tab
        private TextBox txtUser, txtPass;
        private Button btnLogin;
        private Label lblLoginStatus;

        private void SetupLoginTab()
        {
            var lblu = new Label() { Text = "Username:", Left = 20, Top = 20 };
            txtUser = new TextBox() { Left = 120, Top = 18, Width = 200 }; //was left 100
            var lblp = new Label() { Text = "Password:", Left = 20, Top = 60 };
            txtPass = new TextBox() { Left = 120, Top = 58, Width = 200, UseSystemPasswordChar = true };
            btnLogin = new Button() { Text = "Login (Hardcoded)", Left = 100, Top = 100, Width = 200 };
            btnLogin.Click += BtnLogin_Click;
            lblLoginStatus = new Label() { Left = 100, Top = 140, Width = 700 };

            var lblInfo = new Label() { Left = 350, Top = 18, Width = 500, Height = 80, Text = "Note: This demo includes hard-coded credentials and shows plaintext config in the About tab." };

            tabLogin.Controls.AddRange(new Control[] { lblu, txtUser, lblp, txtPass, btnLogin, lblLoginStatus, lblInfo });
        }

        private void BtnLogin_Click(object sender, EventArgs e)
        {
            // Insecure authentication logic (vulnerability: hardcoded creds & no rate limiting)
            if (txtUser.Text == HardcodedUser && txtPass.Text == HardcodedPass)
            {
                lblLoginStatus.Text = "Login successful (using hard-coded credentials).";
            }
            else
            {
                lblLoginStatus.Text = "Login failed.";
            }
        }
        #endregion

        #region Notes Tab (Plaintext storage)
        private TextBox txtNotes;
        private Button btnSaveNotes, btnLoadNotes;

        private void SetupNotesTab()
        {
            txtNotes = new TextBox() { Left = 20, Top = 20, Width = 800, Height = 380, Multiline = true, ScrollBars = ScrollBars.Vertical };
            btnSaveNotes = new Button() { Left = 20, Top = 420, Text = "Save Notes (plaintext)" };
            btnLoadNotes = new Button() { Left = 180, Top = 420, Text = "Load Notes" };
            btnSaveNotes.Click += BtnSaveNotes_Click;
            btnLoadNotes.Click += BtnLoadNotes_Click;

            tabNotes.Controls.AddRange(new Control[] { txtNotes, btnSaveNotes, btnLoadNotes });
        }

        private void BtnSaveNotes_Click(object sender, EventArgs e)
        {
            // Vulnerability: storing sensitive data in plaintext on disk
            File.WriteAllText(notesFile, txtNotes.Text);
            MessageBox.Show("Notes saved in plaintext to: " + notesFile);
        }

        private void BtnLoadNotes_Click(object sender, EventArgs e)
        {
            if (File.Exists(notesFile))
            {
                txtNotes.Text = File.ReadAllText(notesFile);
            }
            else
            {
                MessageBox.Show("No notes file found.");
            }
        }
        #endregion

        #region Command Execution Tab (Command Injection / OS Command)
        private TextBox txtCmdInput, txtCmdOutput;
        private Button btnRunCmd;

        private void SetupCmdTab()
        {
            var lbl = new Label() { Left = 20, Top = 20, Text = "Enter filename or command to run (insecure: no sanitization)" };
            txtCmdInput = new TextBox() { Left = 20, Top = 45, Width = 600 };
            btnRunCmd = new Button() { Left = 640, Top = 42, Text = "Run" };
            txtCmdOutput = new TextBox() { Left = 20, Top = 85, Width = 800, Height = 320, Multiline = true, ScrollBars = ScrollBars.Vertical };

            btnRunCmd.Click += BtnRunCmd_Click;
            tabCmd.Controls.AddRange(new Control[] { lbl, txtCmdInput, btnRunCmd, txtCmdOutput });
        }

        private void BtnRunCmd_Click(object sender, EventArgs e)
        {
            // Vulnerability: direct use of user input in Process.Start -> command injection / privileged execution
            try
            {
                var arg = txtCmdInput.Text;
                // Intentional: start "notepad.exe" with user-supplied file path (no validation)
                //var pi = new ProcessStartInfo("notepad.exe", arg);
                //var pi = new ProcessStartInfo("cmd.exe", arg);
                var pi = new ProcessStartInfo("cmd.exe", "/k " + arg);
                pi.UseShellExecute = true;
                Process.Start(pi);
                txtCmdOutput.Text = "Started notepad with argument: " + arg;
            }
            catch (Exception ex)
            {
                txtCmdOutput.Text = "Error: " + ex.Message;
            }
        }
        #endregion

        #region Unsafe Deserialization Tab
        private Button btnLoadSerialized, btnSaveSerialized;
        private TextBox txtDeserializeLog;

        private void SetupDeserializeTab()
        {
            btnSaveSerialized = new Button() { Left = 20, Top = 20, Text = "Create sample serialized payload" };
            btnLoadSerialized = new Button() { Left = 220, Top = 20, Text = "Load & Deserialize file" };
            txtDeserializeLog = new TextBox() { Left = 20, Top = 60, Width = 800, Height = 340, Multiline = true, ScrollBars = ScrollBars.Vertical };

            btnSaveSerialized.Click += BtnSaveSerialized_Click;
            btnLoadSerialized.Click += BtnLoadSerialized_Click;

            tabDeserialize.Controls.AddRange(new Control[] { btnSaveSerialized, btnLoadSerialized, txtDeserializeLog });
        }

        private void BtnSaveSerialized_Click(object sender, EventArgs e)
        {
            var payload = new DemoPayload() { Message = "Hello from serialized object" };
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "payload.bin");
            var bf = new BinaryFormatter(); // Insecure API (kept for demo)
            using (var fs = File.Open(path, FileMode.Create))
            {
#pragma warning disable SYSLIB0011
                bf.Serialize(fs, payload);
#pragma warning restore SYSLIB0011
            }
            txtDeserializeLog.Text = "Wrote sample payload to: " + path + "\r\n(This demo uses BinaryFormatter to demonstrate unsafe deserialization.)";
        }

        private void BtnLoadSerialized_Click(object sender, EventArgs e)
        {
            var ofd = new OpenFileDialog();
            ofd.Filter = "Binary files|*.bin;*.dat|All files|*.*";
            if (ofd.ShowDialog() != DialogResult.OK) return;

            try
            {
                var bf = new BinaryFormatter(); // insecure: allows deserialization attacks
                using (var fs = File.Open(ofd.FileName, FileMode.Open))
                {
#pragma warning disable SYSLIB0011
                    var obj = bf.Deserialize(fs);
#pragma warning restore SYSLIB0011
                    txtDeserializeLog.Text = "Deserialized object type: " + obj?.GetType().FullName + "\r\nTo view string properties, ToString(): " + obj?.ToString();
                }
            }
            catch (Exception ex)
            {
                txtDeserializeLog.Text = "Deserialize error: " + ex.Message;
            }
        }
        #endregion

        #region Insecure Updater Tab
        private Button btnCheckUpdate;
        private TextBox txtUpdateLog;

        private void SetupUpdateTab()
        {
            btnCheckUpdate = new Button() { Left = 20, Top = 20, Text = "Check for update (HTTP)" };
            txtUpdateLog = new TextBox() { Left = 20, Top = 60, Width = 800, Height = 340, Multiline = true, ScrollBars = ScrollBars.Vertical };
            btnCheckUpdate.Click += BtnCheckUpdate_Click;
            tabUpdate.Controls.AddRange(new Control[] { btnCheckUpdate, txtUpdateLog });
        }

        private async void BtnCheckUpdate_Click(object sender, EventArgs e)
        {
            // Vulnerability: insecure update from HTTP, no signature verification, no TLS
            var url = "http://insecure-updates.example.com/fake-update.exe"; // intentionally insecure
            txtUpdateLog.Text = "Attempting to download (insecure) from: " + url + "\r\n";

            try
            {
                using (var client = new HttpClient())
                {
                    var bytes = await client.GetByteArrayAsync(url);
                    var target = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "downloaded_update.exe");
                    File.WriteAllBytes(target, bytes);
                    txtUpdateLog.AppendText("Downloaded to: " + target + "\r\n");

                    // Intentional: start the downloaded file (bad idea)
                    txtUpdateLog.AppendText("Attempting to start the downloaded file (unsafe)\r\n");
                    Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                txtUpdateLog.AppendText("Update error: " + ex.Message + "\r\n");
            }
        }
        #endregion


        /*
        #region About / Config Tab (Shows plaintext config & weak crypto example)
        private TextBox txtAbout;
        private Button btnShowConfig, btnWeakHash;

        private void SetupAboutTab()
        {
            txtAbout = new TextBox() { Left = 20, Top = 20, Width = 800, Height = 320, Multiline = true, ScrollBars = ScrollBars.Vertical };
            btnShowConfig = new Button() { Left = 20, Top = 360, Text = "Show plaintext config (insecure)" };
            btnWeakHash = new Button() { Left = 220, Top = 360, Text = "Compute MD5 of input (weak crypto)" };
            btnShowConfig.Click += BtnShowConfig_Click;
            btnWeakHash.Click += BtnWeakHash_Click;

            tabAbout.Controls.AddRange(new Control[] { txtAbout, btnShowConfig, btnWeakHash });
        }

        private void BtnShowConfig_Click(object sender, EventArgs e)
        {
            if (File.Exists(configFile))
            {
                txtAbout.Text = File.ReadAllText(configFile);
            }
            else txtAbout.Text = "No config file present.";
        }

        private void BtnWeakHash_Click(object sender, EventArgs e)
        {
            var input = Microsoft.VisualBasic.Interaction.InputBox("Enter a string to hash with MD5 (weak):", "MD5 Demo", "hello");
            if (string.IsNullOrEmpty(input)) return;
            using (var md5 = MD5.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(input);
                var hash = md5.ComputeHash(bytes);
                txtAbout.Text = BitConverter.ToString(hash).Replace("-", "");
            }
        }
        #endregion
        */


        #region Mitigations (documentation)
        /*
         * MITIGATIONS
         *
         * 1) Hardcoded credentials: remove hard-coded secrets. Use OS-provided secure storage (Windows Credential Manager), or better, use identity providers and secure storage.
         * 2) Plaintext files: encrypt sensitive files at rest, restrict filesystem permissions. Prefer protected storage APIs.
         * 3) SQL Injection: ALWAYS use parameterized queries or ORMs with parameter binding. Never concatenate user input into SQL commands.
         * 4) Command Injection: validate/whitelist inputs. Avoid passing user input to shell commands. If you must, escape carefully and use APIs that accept argument lists.
         * 5) Unsafe deserialization: avoid BinaryFormatter. Use safe formats (JSON) and explicit type-binding, or use libraries that provide safe deserialization.
         * 6) Insecure updates: use HTTPS, validate TLS, host updates on secure servers, and verify signatures (code signing) before executing downloaded code.
         * 7) Weak crypto: avoid MD5/SHA1 for security purposes. Use modern algorithms (SHA256+) and proper key-based HMACs or vetted crypto libraries. Use Windows DPAPI or Azure Key Vault for secrets.
         * 8) File permissions: create data files in user profile directories with restrictive ACLs. Avoid placing secrets in program folder.
         * 9) Logging: avoid logging sensitive data in plaintext. Use redaction and protect log storage.
         * 10) Principle of Least Privilege: run with least necessary privileges and avoid operations requiring admin rights.
         *
         * Use threat modeling, static analysis, and dynamic testing tools to locate and fix issues. Consider using existing intentionally-vulnerable apps (e.g., OWASP Juice Shop for web) as references.
         */
        #endregion
    }
}
