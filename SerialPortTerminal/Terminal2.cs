/* 
 * Project:    SerialPort Terminal
 * Company:    Coad .NET, http://coad.net
 * Author:     Noah Coad, http://coad.net/noah
 * Created:    March 2005
 * 
 * Notes:      This was created to demonstrate how to use the SerialPort control for
 *             communicating with your PC's Serial RS-232 COM Port
 * 
 *             It is for educational purposes only and not sanctified for industrial use. :)
 *             Written to support the blog post article at: http://msmvps.com/blogs/coad/archive/2005/03/23/39466.aspx
 * 
 *             Search for "comport" to see how I'm using the SerialPort control.
 */

#region Namespace Inclusions
using System;
using System.Linq;
using System.Data;
using System.Text;
using System.Drawing;
using System.IO.Ports;
using System.Windows.Forms;
using System.ComponentModel;
using System.Collections.Generic;

using SerialPortTerminal.Properties;
using System.Threading;
using System.IO;
#endregion

namespace SerialPortTerminal
{
  #region Public Enumerations
  public enum DataMode { Text, Hex,Int }
  public enum LogMsgType { Incoming, Outgoing, Normal, Warning, Error };
  #endregion

  public partial class frmTerminal : Form
  {
    #region Local Variables

      // Wave header

      static byte[] RIFF_HEADER = new byte[] { 0x52, 0x49, 0x46, 0x46 };
      static byte[] FORMAT_WAVE = new byte[] { 0x57, 0x41, 0x56, 0x45 };
      static byte[] FORMAT_TAG = new byte[] { 0x66, 0x6d, 0x74, 0x20 };
      static byte[] AUDIO_FORMAT = new byte[] { 0x01, 0x00 };
      static byte[] SUBCHUNK_ID = new byte[] { 0x64, 0x61, 0x74, 0x61 };
      private const int BYTES_PER_SAMPLE = 2;
      private long raz = 0;
      private int sprawdz = 0;

      private List<byte> itemsList = new List<byte>();

    // The main control for communicating through the RS-232 port
    private SerialPort comport = new SerialPort();

    // Various colors for logging info
    private Color[] LogMsgTypeColor = { Color.Blue, Color.Green, Color.Black, Color.Orange, Color.Red };

    // Temp holder for whether a key was pressed
    private bool KeyHandled = false;

		private Settings settings = Settings.Default;
    #endregion

    #region Constructor
    public frmTerminal()
    {
			// Load user settings
			settings.Reload();

      // Build the form
      InitializeComponent();

      // Restore the users settings
      InitializeControlValues();

      // Enable/disable controls based on the current state
      EnableControls();

      // When data is recieved through the port, call this method
      comport.DataReceived += new SerialDataReceivedEventHandler(port_DataReceived);
			comport.PinChanged += new SerialPinChangedEventHandler(comport_PinChanged);
    }

		void comport_PinChanged(object sender, SerialPinChangedEventArgs e)
		{
			// Show the state of the pins
			UpdatePinState();
		}

		private void UpdatePinState()
		{
			this.Invoke(new ThreadStart(() => {
				// Show the state of the pins
				chkCD.Checked = comport.CDHolding;
				chkCTS.Checked = comport.CtsHolding;
				chkDSR.Checked = comport.DsrHolding;
			}));
		}
    #endregion

    #region Local Methods
    
    /// <summary> Save the user's settings. </summary>
    private void SaveSettings()
    {
			settings.BaudRate = int.Parse(cmbBaudRate.Text);
			settings.DataBits = int.Parse(cmbDataBits.Text);
			settings.DataMode = CurrentDataMode;
			settings.Parity = (Parity)Enum.Parse(typeof(Parity), cmbParity.Text);
			settings.StopBits = (StopBits)Enum.Parse(typeof(StopBits), cmbStopBits.Text);
			settings.PortName = cmbPortName.Text;
			settings.ClearOnOpen = chkClearOnOpen.Checked;
			settings.ClearWithDTR = chkClearWithDTR.Checked;

			settings.Save();
    }

    /// <summary> Populate the form's controls with default settings. </summary>
    private void InitializeControlValues()
    {
      cmbParity.Items.Clear(); cmbParity.Items.AddRange(Enum.GetNames(typeof(Parity)));
      cmbStopBits.Items.Clear(); cmbStopBits.Items.AddRange(Enum.GetNames(typeof(StopBits)));

			cmbParity.Text = settings.Parity.ToString();
			cmbStopBits.Text = settings.StopBits.ToString();
			cmbDataBits.Text = settings.DataBits.ToString();
			cmbParity.Text = settings.Parity.ToString();
			cmbBaudRate.Text = settings.BaudRate.ToString();
			CurrentDataMode = settings.DataMode;

			RefreshComPortList();

			chkClearOnOpen.Checked = settings.ClearOnOpen;
			chkClearWithDTR.Checked = settings.ClearWithDTR;

			// If it is still avalible, select the last com port used
			if (cmbPortName.Items.Contains(settings.PortName)) cmbPortName.Text = settings.PortName;
      else if (cmbPortName.Items.Count > 0) cmbPortName.SelectedIndex = cmbPortName.Items.Count - 1;
      else
      {
        MessageBox.Show(this, "There are no COM Ports detected on this computer.\nPlease install a COM Port and restart this app.", "No COM Ports Installed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        this.Close();
      }
    }

    /// <summary> Enable/disable controls based on the app's current state. </summary>
    private void EnableControls()
    {
      // Enable/disable controls based on whether the port is open or not
      gbPortSettings.Enabled = !comport.IsOpen;
      txtSendData.Enabled = btnSend.Enabled = comport.IsOpen;
			//chkDTR.Enabled = chkRTS.Enabled = comport.IsOpen;

      if (comport.IsOpen) btnOpenPort.Text = "&Close Port";
      else btnOpenPort.Text = "&Open Port";
    }

    /// <summary> Send the user's data currently entered in the 'send' box.</summary>
    private void SendData()
    {
      if (CurrentDataMode == DataMode.Text)
      {
        // Send the user's text straight out the port
        comport.Write(txtSendData.Text);

        // Show in the terminal window the user's text
        Log(LogMsgType.Outgoing, txtSendData.Text + "\n");
      }
      else
      {
        try
        {
          // Convert the user's string of hex digits (ex: B4 CA E2) to a byte array
          byte[] data = HexStringToByteArray(txtSendData.Text);

          // Send the binary data out the port
          comport.Write(data, 0, data.Length);

          // Show the hex digits on in the terminal window
          Log(LogMsgType.Outgoing, ByteArrayToHexString(data) + "\n");
        }
        catch (FormatException)
        {
          // Inform the user if the hex string was not properly formatted
          Log(LogMsgType.Error, "Not properly formatted hex string: " + txtSendData.Text + "\n");
        }
      }
      txtSendData.SelectAll();
    }

    /// <summary> Log data to the terminal window. </summary>
    /// <param name="msgtype"> The type of message to be written. </param>
    /// <param name="msg"> The string containing the message to be shown. </param>
    private void Log(LogMsgType msgtype, string msg)
    {
      rtfTerminal.Invoke(new EventHandler(delegate
      {
        rtfTerminal.SelectedText = string.Empty;
        rtfTerminal.SelectionFont = new Font(rtfTerminal.SelectionFont, FontStyle.Bold);
        rtfTerminal.SelectionColor = LogMsgTypeColor[(int)msgtype];
        rtfTerminal.AppendText(msg);
        rtfTerminal.ScrollToCaret();
      }));
    }

    /// <summary> Convert a string of hex digits (ex: E4 CA B2) to a byte array. </summary>
    /// <param name="s"> The string containing the hex digits (with or without spaces). </param>
    /// <returns> Returns an array of bytes. </returns>
    private byte[] HexStringToByteArray(string s)
    {
      s = s.Replace(" ", "");
      byte[] buffer = new byte[s.Length / 2];
      for (int i = 0; i < s.Length; i += 2)
        buffer[i / 2] = (byte)Convert.ToByte(s.Substring(i, 2), 16);
      return buffer;
    }

    /// <summary> Converts an array of bytes into a formatted string of hex digits (ex: E4 CA B2)</summary>
    /// <param name="data"> The array of bytes to be translated into a string of hex digits. </param>
    /// <returns> Returns a well formatted string of hex digits with spacing. </returns>
    private string ByteArrayToHexString(byte[] data)
    {
      StringBuilder sb = new StringBuilder(data.Length * 3);
      foreach (byte b in data)
        sb.Append(Convert.ToString(b, 16).PadLeft(2, '0').PadRight(3, ' '));
      return sb.ToString().ToUpper();
    }
    #endregion

    #region Local Properties
    private DataMode CurrentDataMode
    {
      get
      {
        if (rbHex.Checked) 
            return DataMode.Hex;
        else if (rbInt.Checked)
            return DataMode.Int;
        else return DataMode.Text;
      }
      set
      {
        if (value == DataMode.Text) 
            rbText.Checked = true;
        else if (value == DataMode.Int)
            rbInt.Checked = true;
        else rbHex.Checked = true;
      }
    }
    #endregion

    #region Event Handlers
    private void lnkAbout_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
    {
      // Show the user the about dialog
      (new frmAbout()).ShowDialog(this);
    }
    
    private void frmTerminal_Shown(object sender, EventArgs e)
    {
      Log(LogMsgType.Normal, String.Format("Application Started at {0}\n", DateTime.Now));
    }
    private void frmTerminal_FormClosing(object sender, FormClosingEventArgs e)
    {
      // The form is closing, save the user's preferences
      SaveSettings();
    }

    private void rbText_CheckedChanged(object sender, EventArgs e)
    { if (rbText.Checked) CurrentDataMode = DataMode.Text; }

    private void rbHex_CheckedChanged(object sender, EventArgs e)
    { if (rbHex.Checked) CurrentDataMode = DataMode.Hex; }

    private void rbInt_CheckedChanged(object sender, EventArgs e)
    { if (rbInt.Checked) CurrentDataMode = DataMode.Int; }

    private void cmbBaudRate_Validating(object sender, CancelEventArgs e)
    { int x; e.Cancel = !int.TryParse(cmbBaudRate.Text, out x); }

    private void cmbDataBits_Validating(object sender, CancelEventArgs e)
    { int x; e.Cancel = !int.TryParse(cmbDataBits.Text, out x); }

    private void btnOpenPort_Click(object sender, EventArgs e)
    {
			bool error = false;

      // If the port is open, close it.
      if (comport.IsOpen) comport.Close();
      else
      {
        // Set the port's settings
        comport.BaudRate = int.Parse(cmbBaudRate.Text);
        comport.DataBits = int.Parse(cmbDataBits.Text);
        comport.StopBits = (StopBits)Enum.Parse(typeof(StopBits), cmbStopBits.Text);
        comport.Parity = (Parity)Enum.Parse(typeof(Parity), cmbParity.Text);
        comport.PortName = cmbPortName.Text;

				try
				{
					// Open the port
					comport.Open();
				}
				catch (UnauthorizedAccessException) { error = true; }
				catch (IOException) { error = true; }
				catch (ArgumentException) { error = true; }

				if (error) MessageBox.Show(this, "Could not open the COM port.  Most likely it is already in use, has been removed, or is unavailable.", "COM Port Unavalible", MessageBoxButtons.OK, MessageBoxIcon.Stop);
				else
				{
					// Show the initial pin states
					UpdatePinState();
					chkDTR.Checked = comport.DtrEnable;
					chkRTS.Checked = comport.RtsEnable;
				}
      }

      // Change the state of the form's controls
      EnableControls();

      // If the port is open, send focus to the send data box
			if (comport.IsOpen)
			{
				txtSendData.Focus();
				if (chkClearOnOpen.Checked) ClearTerminal();
			}
    }
    private void btnSend_Click(object sender, EventArgs e)
    { SendData(); }

    private void port_DataReceived(object sender, SerialDataReceivedEventArgs e)
    {
			// If the com port has been closed, do nothing
			if (!comport.IsOpen) return;

            // This method will be called when there is data waiting in the port's buffer

            // Determain which mode (string or binary) the user is in
            // We don't need no DataMode.Text 
            if (true/*CurrentDataMode == DataMode.Hex*/)//DATA FROM STM ALWAYS AS BYTES
            {
                // Obtain the number of bytes waiting in the port's buffer
                //comport.ReadTimeout = 10000;
                int bytes = comport.BytesToRead;
                raz += bytes;
                // Create a byte array buffer to hold the incoming data
                byte[] buffer = new byte[bytes];

                if (bytes != 0)
                {
                    sprawdz = 1;
                    // Read the data from the port and store it in our buffer
                    comport.Read(buffer, 0, bytes);

                    itemsList.AddRange(buffer);
                }


                /*   while ((raz += (bytes = comport.BytesToRead) ) <  50000)
                   {

                       if (bytes > 0)
                       {
                           // Create a byte array buffer to hold the incoming data
                           byte[] buffer = new byte[bytes];

                           //byte[] partBuff = new byte[bytes];
                           // Read the data from the port and store it in our buffer
                           comport.Read(buffer, 0, bytes);
                           itemsList.AddRange(buffer);

                       }
                   }
                   */




                //File.WriteAllBytes(@"C:\Download Zone\SerialPortTerminal\SerialPortTerminal\PCMFILE.wav", buffer);//@@@

                //UInt16 value = BitConverter.ToUInt16(buffer, 0);
                // UInt16[] output = zamien(buffer);
                /*        if (buff2.Length > 0)
                        {
                                using (FileStream fs = new FileStream(@"C:\Download Zone\SerialPortTerminal\SerialPortTerminal\plik.wav", FileMode.Create))
                                {
                                    WriteHeader(fs, buff2.Length, 1, 32000);
                                    fs.Write(buff2, 0, buff2.Length);
                                    fs.Close();
                                    itemsList.Clear();
                                }

                        } */
                //System.Media.SoundPlayer myPlayer = new System.Media.SoundPlayer(@"C:\Users\fr1zer\Downloads\SerialPortTerminal\SerialPortTerminal\PCMFILE.wav");
                //myPlayer.Stream = new MemoryStream();
                //myPlayer.Play();
                //UInt16[] tab = zamien(buffer);
                // Show the user the incoming data in hex format
                Log(LogMsgType.Incoming, ByteArrayToHexString(buffer));
            }
            else
            {

            }


      if (raz >= 65500)
      {
          //byte[] buff2 = itemsList.ToArray();
          if (sprawdz == 1)
          {
              byte[] buff2 = itemsList.ToArray();
              sprawdz = 0;
              using (FileStream fs = new FileStream(@"C:\Download Zone\SerialPortTerminal\SerialPortTerminal\plik.wav", FileMode.Create))
              {
                  WriteHeader(fs, buff2.Length, 1, 16000);
                  fs.Write(buff2, 0, buff2.Length);
                  fs.Close();
                  // itemsList.Clear();
              }

              Log(LogMsgType.Incoming, "zapisalem plik"/*ByteArrayToHexString(itemsList.ToArray())*/);

          }
      }

    }

    public UInt16[] zamien(byte[] tablica)
    {
        UInt16[] tab2 = new UInt16[tablica.Length/2];
        byte[] bin = new byte[2];
        int indeks = 0;

        for (int i = 0; i < tablica.Length; i++)
        {
            bin[0] = tablica[i++];
            bin[1] = tablica[i];
            UInt16 value = BitConverter.ToUInt16(bin, 0);
            tab2[indeks] = value;
            indeks++;

        }

        return tab2;
    }

    public static void WriteHeader(
         System.IO.Stream targetStream,
         int byteStreamSize,
         int channelCount,
         int sampleRate)
    {

        int byteRate = sampleRate * channelCount * BYTES_PER_SAMPLE;
        int blockAlign = channelCount * BYTES_PER_SAMPLE;

        targetStream.Write(RIFF_HEADER, 0, RIFF_HEADER.Length);
        targetStream.Write(PackageInt(byteStreamSize + 42, 4), 0, 4);

        targetStream.Write(FORMAT_WAVE, 0, FORMAT_WAVE.Length);
        targetStream.Write(FORMAT_TAG, 0, FORMAT_TAG.Length);
        targetStream.Write(PackageInt(16, 4), 0, 4);//Subchunk1Size    

        targetStream.Write(AUDIO_FORMAT, 0, AUDIO_FORMAT.Length);//AudioFormat   
        targetStream.Write(PackageInt(channelCount, 2), 0, 2);
        targetStream.Write(PackageInt(sampleRate, 4), 0, 4);
        targetStream.Write(PackageInt(byteRate, 4), 0, 4);
        targetStream.Write(PackageInt(blockAlign, 2), 0, 2);
        targetStream.Write(PackageInt(BYTES_PER_SAMPLE * 8), 0, 2);
        //targetStream.Write(PackageInt(0,2), 0, 2);//Extra param size
        targetStream.Write(SUBCHUNK_ID, 0, SUBCHUNK_ID.Length);
        targetStream.Write(PackageInt(byteStreamSize, 4), 0, 4);
    }

    static byte[] PackageInt(int source, int length = 2)
    {
        if ((length != 2) && (length != 4))
            throw new ArgumentException("length must be either 2 or 4", "length");
        var retVal = new byte[length];
        retVal[0] = (byte)(source & 0xFF);
        retVal[1] = (byte)((source >> 8) & 0xFF);
        if (length == 4)
        {
            retVal[2] = (byte)((source >> 0x10) & 0xFF);
            retVal[3] = (byte)((source >> 0x18) & 0xFF);
        }
        return retVal;
    }

    private void txtSendData_KeyDown(object sender, KeyEventArgs e)
    { 
      // If the user presses [ENTER], send the data now
      if (KeyHandled = e.KeyCode == Keys.Enter) { e.Handled = true; SendData(); } 
    }
    private void txtSendData_KeyPress(object sender, KeyPressEventArgs e)
    { e.Handled = KeyHandled; }
    #endregion

		private void chkDTR_CheckedChanged(object sender, EventArgs e)
		{
			comport.DtrEnable = chkDTR.Checked;
			if (chkDTR.Checked && chkClearWithDTR.Checked) ClearTerminal();
		}

		private void chkRTS_CheckedChanged(object sender, EventArgs e)
		{
			comport.RtsEnable = chkRTS.Checked;
		}

		private void btnClear_Click(object sender, EventArgs e)
		{
			ClearTerminal();
		}

		private void ClearTerminal()
		{
			rtfTerminal.Clear();
		}

		private void tmrCheckComPorts_Tick(object sender, EventArgs e)
		{
			// checks to see if COM ports have been added or removed
			// since it is quite common now with USB-to-Serial adapters
			RefreshComPortList();
		}

		private void RefreshComPortList()
		{
			// Determain if the list of com port names has changed since last checked
			string selected = RefreshComPortList(cmbPortName.Items.Cast<string>(), cmbPortName.SelectedItem as string, comport.IsOpen);

			// If there was an update, then update the control showing the user the list of port names
			if (!String.IsNullOrEmpty(selected))
			{
				cmbPortName.Items.Clear();
				cmbPortName.Items.AddRange(OrderedPortNames());
				cmbPortName.SelectedItem = selected;
			}
		}

		private string[] OrderedPortNames()
		{
			// Just a placeholder for a successful parsing of a string to an integer
			int num;

			// Order the serial port names in numberic order (if possible)
			return SerialPort.GetPortNames().OrderBy(a => a.Length > 3 && int.TryParse(a.Substring(3), out num) ? num : 0).ToArray(); 
		}
		
		private string RefreshComPortList(IEnumerable<string> PreviousPortNames, string CurrentSelection, bool PortOpen)
		{
			// Create a new return report to populate
			string selected = null;

			// Retrieve the list of ports currently mounted by the operating system (sorted by name)
			string[] ports = SerialPort.GetPortNames();

			// First determain if there was a change (any additions or removals)
			bool updated = PreviousPortNames.Except(ports).Count() > 0 || ports.Except(PreviousPortNames).Count() > 0;

			// If there was a change, then select an appropriate default port
			if (updated)
			{
				// Use the correctly ordered set of port names
				ports = OrderedPortNames();

				// Find newest port if one or more were added
				string newest = SerialPort.GetPortNames().Except(PreviousPortNames).OrderBy(a => a).LastOrDefault();

				// If the port was already open... (see logic notes and reasoning in Notes.txt)
				if (PortOpen)
				{
					if (ports.Contains(CurrentSelection)) selected = CurrentSelection;
					else if (!String.IsNullOrEmpty(newest)) selected = newest;
					else selected = ports.LastOrDefault();
				}
				else
				{
					if (!String.IsNullOrEmpty(newest)) selected = newest;
					else if (ports.Contains(CurrentSelection)) selected = CurrentSelection;
					else selected = ports.LastOrDefault();
				}
			}

			// If there was a change to the port list, return the recommended default selection
			return selected;
		}

        private void rtfTerminal_TextChanged(object sender, EventArgs e)
        {

        }
	}
}