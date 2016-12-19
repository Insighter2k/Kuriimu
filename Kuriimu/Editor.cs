﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows.Forms;
using Be.Windows.Forms;
using Kuriimu.Properties;
using KuriimuContract;

namespace Kuriimu
{
	public partial class frmEditor : Form
	{
		private IFileAdapter _fileAdapter = null;
		private IGameHandler _gameHandler = null;
		private bool _fileOpen = false;
		private bool _hasChanges = false;

		private Dictionary<string, IFileAdapter> _fileAdapters = null;
		private Dictionary<string, IGameHandler> _gameHandlers = null;

		private BindingList<IEntry> _entries = null;

		public frmEditor()
		{
			InitializeComponent();
		}

		private void Editor_Load(object sender, EventArgs e)
		{
			Icon = Resources.kuriimu;

			// Load Plugins
			Console.WriteLine("Loading plugins...");

			_fileAdapters = new Dictionary<string, IFileAdapter>();
			foreach (IFileAdapter fileAdapter in PluginLoader<IFileAdapter>.LoadPlugins(Settings.Default.PluginDirectory, "file*.dll"))
				_fileAdapters.Add(fileAdapter.Name, fileAdapter);

			_gameHandlers = Tools.LoadGameHandlers(tsbGameSelect, Resources.game_none, tsbGameSelect_SelectedIndexChanged);

			Tools.DoubleBuffer((Control)lstEntries, true);

			UpdateForm();
		}

		private void openToolStripMenuItem_Click(object sender, EventArgs e)
		{
			ConfirmOpenFile();
		}

		private void tsbOpen_Click(object sender, EventArgs e)
		{
			ConfirmOpenFile();
		}

		private void saveToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SaveFile();
		}

		private void tsbSave_Click(object sender, EventArgs e)
		{
			SaveFile();
		}

		private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SaveFile(true);
		}

		private void tsbSaveAs_Click(object sender, EventArgs e)
		{
			SaveFile(true);
		}

		private void exitToolStripMenuItem_Click(object sender, EventArgs e)
		{
			Close();
		}

		private void findToolStripMenuItem_Click(object sender, EventArgs e)
		{
			frmSearch search = new frmSearch();
			search.Entries = _fileAdapter.Entries;
			search.ShowDialog();

			if (search.Selected != null)
			{
				lstEntries.SelectedItem = search.Selected;

				if (txtEdit.Text.Contains(Settings.Default.FindWhat))
				{
					txtEdit.SelectionStart = txtEdit.Text.IndexOf(Settings.Default.FindWhat);
					txtEdit.SelectionLength = Settings.Default.FindWhat.Length;
					txtEdit.Focus();

					SelectInHex();
				}
			}
		}

		private void tsbFind_Click(object sender, EventArgs e)
		{
			findToolStripMenuItem_Click(sender, e);
		}

		private void propertiesToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (_fileAdapter.ShowProperties(Resources.kuriimu))
			{
				_hasChanges = true;
				UpdateForm();
			}
		}

		private void tsbFileProperties_Click(object sender, EventArgs e)
		{
			propertiesToolStripMenuItem_Click(sender, e);
		}

		private void gBATempToolStripMenuItem_Click(object sender, EventArgs e)
		{
			System.Diagnostics.Process.Start("http://gbatemp.net/threads/release-kuriimu-a-general-purpose-game-translation-toolkit-for-authors-of-fan-translations.452375/");
		}

		private void gitHubToolStripMenuItem_Click(object sender, EventArgs e)
		{
			System.Diagnostics.Process.Start("https://github.com/Icyson55/Kuriimu");
		}

		private void ConfirmOpenFile(string filename = "")
		{
			DialogResult dr = DialogResult.No;

			if (_fileOpen && _hasChanges)
				dr = MessageBox.Show("You have unsaved changes in " + FileName() + ". Save changes before opening another file?", "Unsaved Changes", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);

			switch (dr)
			{
				case DialogResult.Yes:
					dr = SaveFile();
					if (dr == DialogResult.OK) OpenFile(filename);
					break;
				case DialogResult.No:
					OpenFile(filename);
					break;
			}
		}

		private void OpenFile(string filename = "")
		{
			OpenFileDialog ofd = new OpenFileDialog();
			ofd.InitialDirectory = Settings.Default.LastDirectory;

			// Supported Types
			List<string> types = new List<string>();
			foreach (string key in _fileAdapters.Keys)
				types.Add(_fileAdapters[key].Description + " (" + _fileAdapters[key].Extension + ")|" + _fileAdapters[key].Extension);
			types.Add("All Files (*.*)|*.*");
			ofd.Filter = string.Join("|", types.ToArray());

			DialogResult dr = DialogResult.OK;

			if (filename == string.Empty)
			{
				dr = ofd.ShowDialog();
				filename = ofd.FileName;
			}

			if (dr == DialogResult.OK)
			{
				try
				{
					_fileAdapter = SelectFileAdapter(filename);
					if (_fileAdapter != null)
					{
						_fileAdapter.Load(filename);
						_fileOpen = true;
						_hasChanges = false;
						LoadEntries();
						Settings.Default.LastDirectory = new FileInfo(filename).DirectoryName;
						Settings.Default.Save();
						Settings.Default.Reload();
					}
				}
				catch (Exception ex)
				{
					MessageBox.Show(ex.ToString(), ex.Message, MessageBoxButtons.OK);
					_fileOpen = false;
					_hasChanges = false;
				}

				UpdateForm();
			}
		}

		private DialogResult SaveFile(bool saveAs = false)
		{
			SaveFileDialog sfd = new SaveFileDialog();
			DialogResult dr = DialogResult.OK;

			sfd.Filter = _fileAdapter.Description + " (" + _fileAdapter.Extension + ")|" + _fileAdapter.Extension;

			if (_fileAdapter.FileInfo == null || saveAs)
			{
				sfd.InitialDirectory = Settings.Default.LastDirectory;
				dr = sfd.ShowDialog();
			}

			if ((_fileAdapter.FileInfo == null || saveAs) && dr == DialogResult.OK)
			{
				_fileAdapter.FileInfo = new FileInfo(sfd.FileName);
				Settings.Default.LastDirectory = new FileInfo(sfd.FileName).DirectoryName;
				Settings.Default.Save();
				Settings.Default.Reload();
			}

			if (dr == DialogResult.OK)
			{
				_fileAdapter.Save(_fileAdapter.FileInfo.FullName);
				_hasChanges = false;
				UpdateForm();
			}

			return dr;
		}

		private IFileAdapter SelectFileAdapter(string filename)
		{
			IFileAdapter result = null;
			string extension = new FileInfo(filename).Extension;

			foreach (string key in _fileAdapters.Keys)
			{
				if (_fileAdapters[key].Extension.EndsWith(extension))
				{
					result = _fileAdapters[key];
					break;
				}
			}

			return result;
		}

		private void LoadEntries()
		{
			IEntry selectedItem = (IEntry)lstEntries.SelectedItem;

			_entries = new BindingList<IEntry>(_fileAdapter.Entries);
			lstEntries.DataSource = _entries;

			if (selectedItem != null && lstEntries.Items.Contains(selectedItem))
				lstEntries.SelectedItem = selectedItem;
			else if(lstEntries.Items.Count > 0)
				lstEntries.SelectedIndex = 0;
		}

		// Utilities
		private void UpdateTextView()
		{
			IEntry entry = (IEntry)lstEntries.SelectedItem;

			if (_gameHandler != null)
			{
				txtEdit.Text = _gameHandler.GetString(entry.EditedText, entry.Encoding).Replace("\0", "<null>").Replace("\n", "\r\n");
				txtOriginal.Text = _gameHandler.GetString(entry.OriginalText, entry.Encoding).Replace("\0", "<null>").Replace("\n", "\r\n");
			}
			else
			{
				txtEdit.Text = entry.EditedTextString.Replace("\0", "<null>").Replace("\n", "\r\n");
				txtOriginal.Text = entry.OriginalTextString.Replace("\0", "<null>").Replace("\n", "\r\n");
			}

			if (!entry.IsResizable)
				txtEdit.MaxLength = entry.MaxLength;
		}

		private void UpdateHexView()
		{
			DynamicFileByteProvider dfbp = null;

			try
			{
				IEntry ent = (IEntry)lstEntries.SelectedItem;
				MemoryStream strm = new MemoryStream(ent.EditedText);

				dfbp = new DynamicFileByteProvider(strm);
				dfbp.Changed += new EventHandler(hbxEdit_Changed);
			}
			catch (Exception)
			{ }

			hbxHexView.ByteProvider = dfbp;
		}

		private void UpdateForm()
		{
			Text = Settings.Default.ApplicationName + " Editor " + Settings.Default.ApplicationVersion + (FileName() != string.Empty ? " - " + FileName() : string.Empty) + (_hasChanges ? "*" : string.Empty);

			if (_fileOpen)
				tslEntries.Text = _fileAdapter.Entries.Count + " Entries";
			else
				tslEntries.Text = "Entries";

			bool itemSelected = lstEntries.SelectedIndex >= 0;

			splMain.Enabled = _fileOpen;
			splContent.Enabled = _fileOpen;
			splText.Enabled = _fileOpen;
			splPreview.Enabled = _fileOpen;

			saveToolStripMenuItem.Enabled = _fileOpen && _fileAdapter.CanSave;
			tsbSave.Enabled = _fileOpen && _fileAdapter.CanSave;
			saveAsToolStripMenuItem.Enabled = _fileOpen && _fileAdapter.CanSave;
			tsbSaveAs.Enabled = _fileOpen && _fileAdapter.CanSave;
			findToolStripMenuItem.Enabled = _fileOpen;
			tsbFind.Enabled = _fileOpen;
			propertiesToolStripMenuItem.Enabled = _fileOpen && _fileAdapter.FileHasExtendedProperties;
			tsbProperties.Enabled = _fileOpen && _fileAdapter.FileHasExtendedProperties;

			tsbEntryAdd.Enabled = _fileOpen && _fileAdapter.CanAddEntries;
			tsbEntryRename.Enabled = itemSelected;
			tsbEntryProperties.Enabled = _fileAdapter.EntriesHaveExtendedProperties && itemSelected;
			tsbGameSelect.Enabled = itemSelected;

			lstEntries.Enabled = _fileOpen;

			txtEdit.Enabled = itemSelected;
			txtOriginal.Enabled = itemSelected;
			hbxHexView.Enabled = itemSelected;
		}

		private string FileName()
		{
			return _fileAdapter == null || _fileAdapter.FileInfo == null ? string.Empty : _fileAdapter.FileInfo.Name;
		}

		// Toolbar
		private void tsbEntryAdd_Click(object sender, EventArgs e)
		{
			IEntry entry = _fileAdapter.NewEntry();

			frmName name = new frmName(entry);

			if (name.ShowDialog() == DialogResult.OK && name.NameChanged)
			{
				_hasChanges = true;
				_fileAdapter.AddEntry(entry);
				LoadEntries();

				if (lstEntries.Items.Contains(entry))
					lstEntries.SelectedItem = entry;

				UpdateForm();
			}
		}

		private void tsbEntryRename_Click(object sender, EventArgs e)
		{
			IEntry entry = (IEntry)lstEntries.SelectedItem;

			frmName name = new frmName(entry);

			if (name.ShowDialog() == DialogResult.OK && name.NameChanged)
			{
				_hasChanges = true;
				_entries.ResetBindings();
				lstEntries.SelectedItem = entry;
				UpdateForm();
			}
		}

		private void tsbEntryProperties_Click(object sender, EventArgs e)
		{
			IEntry entry = (IEntry)lstEntries.SelectedItem;
			if (_fileAdapter.ShowEntryProperties(entry, Resources.kuriimu))
			{
				_hasChanges = true;
				UpdateForm();
			}
		}

		private void tsbGameSelect_SelectedIndexChanged(object sender, EventArgs e)
		{
			ToolStripItem tsi = (ToolStripItem)sender;

			if (tsi.Text == "No Game")
				_gameHandler = null;
			else
				_gameHandler = _gameHandlers[((ToolStripItem)sender).Text];

			tsbGameSelect.Text = tsi.Text;
			tsbGameSelect.Image = tsi.Image;

			lstEntries_SelectedIndexChanged(sender, e);
		}

		// List
		private void lstEntries_SelectedIndexChanged(object sender, EventArgs e)
		{
			UpdateTextView();
			UpdateHexView();
			UpdateForm();
		}

		private void lstEntries_KeyDown(object sender, KeyEventArgs e)
		{
			if (lstEntries.Focused && (e.KeyCode == Keys.Enter))
				tsbEntryProperties_Click(sender, e);
		}

		private void lstEntries_DoubleClick(object sender, EventArgs e)
		{
			tsbEntryProperties_Click(sender, e);
		}

		// Text
		private void txtEdit_KeyUp(object sender, KeyEventArgs e)
		{
			IEntry entry = (IEntry)lstEntries.SelectedItem;
			string next = string.Empty;
			string previous = string.Empty;

			if (_gameHandler != null)
			{
				previous = _gameHandler.GetString(entry.EditedText, entry.Encoding);
				next = txtEdit.Text.Replace("<null>", "\0").Replace("\r\n", "\n");
				entry.EditedText = _gameHandler.GetBytes(next, entry.Encoding);
			}
			else
			{
				previous = entry.EditedTextString;
				next = txtEdit.Text.Replace("<null>", "\0").Replace("\r\n", "\n");
				entry.EditedText = entry.Encoding.GetBytes(next);
			}

			UpdateHexView();
			SelectInHex();

			if (next != previous)
				_hasChanges = true;

			UpdateForm();
		}

		private void txtEdit_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.Control & e.KeyCode == Keys.A)
				txtEdit.SelectAll();
		}

		private void txtEdit_MouseUp(object sender, MouseEventArgs e)
		{
			SelectInHex();
		}

		private void txtEdit_TextChanged(object sender, EventArgs e)
		{
			SelectInHex();
		}

		private void SelectInHex()
		{
			// Magic that is not perfect
			IEntry entry = (IEntry)lstEntries.SelectedItem;
			int selectionStart = txtEdit.SelectionStart * (entry.Encoding.IsSingleByte ? 1 : 2);
			int selectionLength = txtEdit.SelectionLength * (entry.Encoding.IsSingleByte ? 1 : 2);

			if (_gameHandler != null)
				selectionLength = entry.Encoding.GetString(_gameHandler.GetBytes(txtEdit.SelectedText.Replace("<null>", "\0"), entry.Encoding)).Length * (entry.Encoding.IsSingleByte ? 1 : 2);
			else
				selectionLength = txtEdit.SelectedText.Replace("<null>", "\0").Length * (entry.Encoding.IsSingleByte ? 1 : 2);

			hbxHexView.SelectionStart = selectionStart;
			hbxHexView.SelectionLength = selectionLength;
		}

		protected void hbxEdit_Changed(object sender, EventArgs e)
		{
			DynamicFileByteProvider dfbp = (DynamicFileByteProvider)sender;

			IEntry entry = (IEntry)lstEntries.SelectedItem;
			List<byte> bytes = new List<byte>();
			for (int i = 0; i < (int)dfbp.Length; i++)
				bytes.Add(dfbp.ReadByte(i));
			entry.EditedText = bytes.ToArray();

			UpdateTextView();

			if (txtEdit.Text != txtOriginal.Text)
				_hasChanges = true;

			UpdateForm();
		}
	}
}