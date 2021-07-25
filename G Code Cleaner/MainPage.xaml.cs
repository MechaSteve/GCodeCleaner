using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace G_Code_Cleaner
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private Windows.Storage.StorageFile openFile;
        private Windows.Storage.StorageFile saveFile;
        private Decimal lastPosX, lastPosY, lastPosZ = 0;
        private Decimal currPosX, currPosY, currPosZ = 0;
        private Decimal lastMode, currMode, nextMode = 0;
        private Decimal currUnitX, lastUnitX = 1;
        private Decimal currUnitY, currUnitZ = 0;
        private Decimal lastUnitY, lastUnitZ = 0;

        public MainPage()
        {
            this.InitializeComponent();
        }

        private async void LoadAFileButton_Click(object sender, RoutedEventArgs e)
        {
            FileOpenPicker openPicker = new FileOpenPicker();
            openPicker.ViewMode = PickerViewMode.List;
            openPicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            openPicker.FileTypeFilter.Add(".nc");
            openPicker.FileTypeFilter.Add(".txt");
            openPicker.FileTypeFilter.Add(".gcode");

            openFile = await openPicker.PickSingleFileAsync();
        }

        private async void SaveAFileButton_Click(object sender, RoutedEventArgs e)
        {
            FileSavePicker savePicker = new FileSavePicker();
            // Dropdown of file types the user can save the file as
            savePicker.FileTypeChoices.Add("NC Program", new List<string>() { ".nc" });
            savePicker.FileTypeChoices.Add("Plain Text", new List<string>() { ".txt" });
            savePicker.FileTypeChoices.Add("G Code", new List<string>() { ".gcode" });
            saveFile = await savePicker.PickSaveFileAsync();
        }

        private async void ProcessFileButton_Click(object sender, RoutedEventArgs e)
        {

            if ((openFile != null) && (saveFile != null))
            {
                // Prevent updates to the remote version of the file until
                // we finish making changes and call CompleteUpdatesAsync.
                Windows.Storage.CachedFileManager.DeferUpdates(saveFile);
                var fileOutStream = await saveFile.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite);
                var outputStream = fileOutStream.GetOutputStreamAt(0);
                var dataWriter = new DataWriter(outputStream);

                //Setup the read Stream
                var fileInStream = await openFile.OpenAsync(Windows.Storage.FileAccessMode.Read);
                var inStream = fileInStream.AsStreamForRead();
                var streamReader = new StreamReader(inStream);

                bool moreToRead = true;
                ulong lineCounter = 0;
                ulong removeCounter = 0;
                bool coordFound = false;
                string currBlock = "";
                               
                
                while (moreToRead)
                {
                    string nextBlock = await streamReader.ReadLineAsync();
                    if (nextBlock == null) moreToRead = false;
                    else
                    {
                        bool specialWords = false;
                        bool longMove = false;
                        bool hasCoords = false;
                        bool hasValueX = false, hasValueY = false, hasValueZ = false;
                        Decimal minimumDist = 0;
                        Decimal.TryParse(tbMinimum.Text, out minimumDist);
                        lineCounter++;
                        List<string> currBlockWords = new List<string>(currBlock.Split(' '));
                        if (currBlock.Contains("G")) specialWords = true;
                        if (currBlock.Contains("M")) specialWords = true;
                        if (currBlock.Contains("F")) specialWords = true;
                        if (currBlock.Contains("S")) specialWords = true;
                        if (currBlock.Contains("I")) specialWords = true;
                        if (currBlock.Contains("J")) specialWords = true;
                        if (currBlock.Contains("K")) specialWords = true;
                        if (currBlock.Contains("R")) specialWords = true;
                        if (currBlock.Contains("%")) specialWords = true;
                        if (currBlock.Contains("(")) specialWords = true;
                        if (!coordFound)
                        {
                            if (currBlock.Contains("X")) coordFound = true;
                            if (currBlock.Contains("Y")) coordFound = true;
                            if (currBlock.Contains("Z")) coordFound = true;
                            if (coordFound) specialWords = true;
                        }

                        foreach (string s in currBlockWords)
                        {
                            if (s.Contains("G"))
                            {
                                Decimal modeValue = -1;
                                Decimal.TryParse(s.Trim().TrimStart('G'), out modeValue);
                                if ((modeValue >= 0) && (modeValue <= 3))
                                {
                                    currMode = modeValue;
                                }
                            }
                            if (s.Contains("X"))
                            {
                                Decimal.TryParse(s.Trim().TrimStart('X'), out currPosX);
                                hasCoords = true;
                                hasValueX = true;
                            }
                            if (s.Contains("Y"))
                            {
                                Decimal.TryParse(s.Trim().TrimStart('Y'), out currPosY);
                                hasCoords = true;
                                hasValueY = true;
                            }
                            if (s.Contains("Z"))
                            {
                                Decimal.TryParse(s.Trim().TrimStart('Z'), out currPosZ);
                                hasCoords = true;
                                hasValueZ = true;
                            }
                        }

                        if (nextBlock.Contains("G"))
                        {
                            List<string> nextBlockWords = new List<string>(nextBlock.Split(' '));
                            foreach (string s in nextBlockWords)
                            {
                                if (s.Contains("G"))
                                {
                                    Decimal modeValue = -1;
                                    Decimal.TryParse(s.Trim().TrimStart('G'), out modeValue);
                                    if ((modeValue >= 0) && (modeValue <= 3))
                                    {
                                        nextMode = modeValue;
                                    }
                                }

                            }
                        }
                        else
                        {
                            nextMode = currMode;
                        }

                        if (hasValueX && !nextBlock.Contains("X")) specialWords = true;
                        if (hasValueY && !nextBlock.Contains("Y")) specialWords = true;
                        if (hasValueZ && !nextBlock.Contains("Z")) specialWords = true;


                        Decimal dX = (currPosX - lastPosX);
                        Decimal dY = (currPosY - lastPosY);
                        Decimal dZ = (currPosZ - lastPosZ);
                        Decimal dist = (decimal)Math.Sqrt((double)((dX * dX) + (dY * dY) + (dZ * dZ)));
                        if (dist >= minimumDist) longMove = true;

                        if (longMove || specialWords || (lastMode != currMode) || (nextMode != currMode))
                        {
                            dataWriter.WriteString(currBlock + Environment.NewLine);
                            if (hasCoords)
                            {
                                lastPosX = currPosX;
                                lastPosY = currPosY;
                                lastPosZ = currPosZ;
                            }
                            lastMode = currMode;
                        }
                        else
                        {
                            removeCounter++;
                        }
                    }

                    currBlock = nextBlock;

                    tbLinesRead.Text = lineCounter.ToString();
                    tbLinesRemoved.Text = removeCounter.ToString();
                    
                }

                //write the last line
                dataWriter.WriteString(currBlock + Environment.NewLine);


                //Write all to the output stream
                await dataWriter.StoreAsync();
                //write all to the file
                await outputStream.FlushAsync();

                // Let Windows know that we're finished changing the file so
                // the other app can update the remote version of the file.
                // Completing updates may require Windows to ask for user input.
                Windows.Storage.Provider.FileUpdateStatus status =
                    await Windows.Storage.CachedFileManager.CompleteUpdatesAsync(saveFile);

            }

        }

    }
}
