﻿////////////////////////////////////////////////////////////////////////////////// 
//                                                                              //
//      Copyright (C) 2005-2014 nzsjb                                           //
//                                                                              //
//  This Program is free software; you can redistribute it and/or modify        //
//  it under the terms of the GNU General Public License as published by        //
//  the Free Software Foundation; either version 2, or (at your option)         //
//  any later version.                                                          //
//                                                                              //
//  This Program is distributed in the hope that it will be useful,             //
//  but WITHOUT ANY WARRANTY; without even the implied warranty of              //
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the                //
//  GNU General Public License for more details.                                //
//                                                                              //
//  You should have received a copy of the GNU General Public License           //
//  along with GNU Make; see the file COPYING.  If not, write to                //
//  the Free Software Foundation, 675 Mass Ave, Cambridge, MA 02139, USA.       //
//  http://www.gnu.org/copyleft/gpl.html                                        //
//                                                                              //  
//////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Threading;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.IO;
using System.Text;
using System.Globalization;

using DomainObjects;
using DirectShow;
using DVBServices;
using XmltvParser;
using MxfParser;
using SatIp;
using VBox;
using NetworkProtocols;

namespace EPGCentre
{
    internal partial class CollectorParametersControl : UserControl, IUpdateControl
    {
        /// <summary>
        /// Get the general window heading for the data.
        /// </summary>
        public string Heading { get { return ("EPG Centre - Collection Parameters - "); } }
        /// <summary>
        /// Get the default directory.
        /// </summary>
        public string DefaultDirectory { get { return (RunParameters.DataDirectory); } }
        /// <summary>
        /// Get the default output file name.
        /// </summary>
        public string DefaultFileName { get { return ("EPG Collector"); } }
        /// <summary>
        /// Get the save file filter.
        /// </summary>
        public string SaveFileFilter { get { return ("INI Files (*.ini)|*.ini"); } }
        /// <summary>
        /// Get the save file title.
        /// </summary>
        public string SaveFileTitle { get { return ("Save EPG Collector Parameter File"); } }
        /// <summary>
        /// Get the save file suffix.
        /// </summary>
        public string SaveFileSuffix { get { return ("ini"); } }

        /// <summary>
        /// Return true if file is new; false otherwise.
        /// </summary>
        public bool NewFile { get { return (newFile); } }

        /// <summary>
        /// Return the state of the data set.
        /// </summary>
        public DataState DataState { get { return (hasDataChanged()); } }

        private delegate DialogResult ShowMessage(string message, MessageBoxButtons buttons, MessageBoxIcon icon);

        private RunParameters runParameters;

        private string currentFileName;
        private RunParameters originalData;
        private bool newFile;

        private const int timeoutLock = 10;
        private const int timeoutCollection = 300;
        private const int timeoutRetries = 5;
        private const int bufferSize = 50;
        private const int bufferFills = 1;

        private BackgroundWorker workerScanStations;
        private AutoResetEvent resetEvent = new AutoResetEvent(false);
        private Collection<TuningFrequency> scanningFrequencies;

        private BindingList<TVStation> bindingList;
        private BindingList<ImportChannelChange> xmltvChannelBindingList;

        private string sortedColumnName;
        private string sortedKeyName;
        private bool sortedAscending;

        private bool satelliteUsed = false;
        private bool terrestrialUsed = false;
        private bool cableUsed = false;
        private bool atscUsed = false;
        private bool clearQamUsed = false;
        private bool isdbsUsed = false;
        private bool isdbtUsed = false;

        private bool currentFrequencyChanging;
        private SatelliteFrequency currentSatelliteFrequency;
        private TerrestrialFrequency currentTerrestrialFrequency;
        private CableFrequency currentCableFrequency;
        private AtscFrequency currentAtscFrequency;
        private ClearQamFrequency currentClearQamFrequency;
        private ISDBSatelliteFrequency currentISDBSatelliteFrequency;
        private ISDBTerrestrialFrequency currentISDBTerrestrialFrequency;
        private FileFrequency currentFileFrequency;
        private StreamFrequency currentStreamFrequency;

        private static string currentXmltvOutputPath;
        private static string currentAreaChannelOutputPath;
        private static string currentBladeRunnerOutputPath;
        private static string currentSageTVOutputPath;
        private static string currentLookupBasePath;
        private static string currentXmltvImportPath;
        private static string currentChannelLogoPath;

        private bool ignoreTuningErrors;

        internal CollectorParametersControl()
        {
            InitializeComponent();

            Satellite.Load();
            TerrestrialProvider.Load();
            CableProvider.Load();
            AtscProvider.Load();
            ClearQamProvider.Load();
            ISDBSatelliteProvider.Load();
            ISDBTerrestrialProvider.Load();
        }

        internal void Process()
        {
            runParameters = new RunParameters(ParameterSet.Collector, RunType.Centre);

            currentFileName = null;
            newFile = true;

            start();
        }

        internal void Process(string fileName, bool newFile)
        {
            currentFileName = fileName;
            this.newFile = newFile;

            runParameters = new RunParameters(ParameterSet.Collector, RunType.Centre);

            Cursor.Current = Cursors.WaitCursor;
            ExitCode reply = runParameters.Process(fileName);
            Cursor.Current = Cursors.Arrow;

            if (reply != ExitCode.OK)
            {
                MessageBox.Show(runParameters.LastError + Environment.NewLine + Environment.NewLine +
                    "Some parameters may not have been processed.", "EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            originalData = runParameters.Clone();
            start();
        }

        private void start()
        {
            tbcDeliverySystem.TabPages.RemoveByKey("tbpISDBSatellite");
            tbcDeliverySystem.TabPages.RemoveByKey("tbpISDBTerrestrial");

            tbcParameters.SelectedTab = tbcParameters.TabPages[0];

            InitializeTuningTab();
            InitializeOutputTab();
            InitializeFilesTab();
            InitializeChannelsTab();
            InitializeTimeShiftTab();
            InitializeFiltersTab();
            InitializeRepeatsTab();
            InitializeAdvancedTab();
            InitializeLookupTab();
            InitializeDiagnosticsTab();
            InitializeUpdateTab();
            InitializeXmltvTab();
            InitializeEditTab();
        }

        private void InitializeTuningTab()
        {
            currentSatelliteFrequency = null;
            currentTerrestrialFrequency = null;
            currentCableFrequency = null;
            currentAtscFrequency = null;
            currentClearQamFrequency = null;
            currentISDBSatelliteFrequency = null;
            currentISDBTerrestrialFrequency = null;
            currentFileFrequency = null;
            currentStreamFrequency = null;

            InitializeSatelliteTab();
            InitializeTerrestrialTab();
            InitializeCableTab();
            InitializeAtscTab();
            InitializeClearQamTab();
            InitializeISDBSTab();
            InitializeISDBTTab();
            InitializeFileTab();
            InitializeStreamTab();

            lvSelectedFrequencies.Items.Clear();
            lbScanningFrequencies.Items.Clear();

            foreach (TuningFrequency tuningFrequency in runParameters.FrequencyCollection)
            {
                ListViewItem item;

                if (tuningFrequency.TunerType != TunerType.File && tuningFrequency.TunerType != TunerType.Stream)
                    item = new ListViewItem(tuningFrequency.ToString());
                else
                    item = new ListViewItem(string.Empty);

                item.Tag = tuningFrequency;

                if (tuningFrequency.Provider != null && tuningFrequency.Provider.Name != null)
                    item.SubItems.Add(tuningFrequency.Provider.Name);

                switch (tuningFrequency.TunerType)
                {
                    case TunerType.Satellite:
                        item.SubItems.Add("Satellite");
                        break;
                    case TunerType.Terrestrial:
                        item.SubItems.Add("Terrestrial");
                        break;
                    case TunerType.Cable:
                        item.SubItems.Add("Cable");
                        break;
                    case TunerType.ATSC:
                        item.SubItems.Add("ATSC Terrestrial");
                        break;
                    case TunerType.ATSCCable:
                        item.SubItems.Add("ATSC Cable");
                        break;
                    case TunerType.ClearQAM:
                        item.SubItems.Add("Clear QAM");
                        break;
                    case TunerType.ISDBS:
                        item.SubItems.Add("ISDB Satellite");
                        break;
                    case TunerType.ISDBT:
                        item.SubItems.Add("ISDB Terrestrial");
                        break;
                    case TunerType.File:
                        item.SubItems.Add(((FileFrequency)tuningFrequency).Path);
                        item.SubItems.Add("File");
                        break;
                    case TunerType.Stream:
                        item.SubItems.Add(((StreamFrequency)tuningFrequency).ToString());
                        item.SubItems.Add("Stream");
                        break;
                    default:
                        item.SubItems.Add("Unknown");
                        break;
                }

                item.SubItems.Add(tuningFrequency.CollectionType.ToString());

                if (tuningFrequency.TunerType == TunerType.Satellite)
                {
                    SatelliteFrequency satelliteFrequency = tuningFrequency as SatelliteFrequency;
                    item.SubItems.Add(satelliteFrequency.SatelliteDish.LNBLowBandFrequency.ToString());
                    item.SubItems.Add(satelliteFrequency.SatelliteDish.LNBHighBandFrequency.ToString());
                    item.SubItems.Add(satelliteFrequency.SatelliteDish.LNBSwitchFrequency.ToString());
                    if (satelliteFrequency.DiseqcRunParamters.DiseqcSwitch != null)
                        item.SubItems.Add(satelliteFrequency.DiseqcRunParamters.DiseqcSwitch);
                    else
                        item.SubItems.Add(string.Empty);
                    if (satelliteFrequency.LNBConversion)
                        item.SubItems.Add(satelliteFrequency.SatelliteDish.LNBType.ToString());
                    else
                        item.SubItems.Add(string.Empty);

                    if (satelliteFrequency.SatIpFrontend != -1)
                        item.SubItems.Add(satelliteFrequency.SatIpFrontend.ToString());
                    else
                        item.SubItems.Add(string.Empty);
                }
                else
                {
                    item.SubItems.Add(string.Empty);
                    item.SubItems.Add(string.Empty);
                    item.SubItems.Add(string.Empty);
                    item.SubItems.Add(string.Empty);
                    item.SubItems.Add(string.Empty);

                    if (tuningFrequency.SatIpFrontend != -1)
                        item.SubItems.Add(tuningFrequency.SatIpFrontend.ToString());
                    else
                        item.SubItems.Add(string.Empty);
                }

                lvSelectedFrequencies.Items.Add(item);
                lbScanningFrequencies.Items.Add(tuningFrequency);
            }

            btDelete.Enabled = false;
            btChange.Enabled = false;
        }

        private void InitializeSatelliteTab()
        {
            int tunerCount = 0;

            foreach (Tuner tuner in Tuner.TunerCollection)
            {
                if (!tuner.Name.ToUpper().Contains("DVBLINK"))
                {
                    if (tuner.Supports(TunerNodeType.Satellite))
                        satelliteUsed = true;

                    tunerCount++;
                }
            }

            if (!satelliteUsed && tunerCount > 0)
            {
                tbcDeliverySystem.TabPages.RemoveByKey("tbpSatellite");
                return;
            }

            if (cboSatellite.Items.Count == 0)
            {
                foreach (Satellite satellite in Satellite.Providers)
                    cboSatellite.Items.Add(satellite);
            }
            else
                cboSatellite.SelectedItem = null;

            if (cboDVBSCollectionType.Items.Count == 0)
            {
                foreach (CollectionType collectionType in System.Enum.GetValues(typeof(CollectionType)))
                    cboDVBSCollectionType.Items.Add(collectionType);
            }

            if (cboLNBType.Items.Count == 0)
            {
                foreach (LNBType lnbType in LNBType.LNBTypes)
                    cboLNBType.Items.Add(lnbType);
            }

            udDvbsSatIpFrontend.Text = udDvbsSatIpFrontend.Items[0].ToString();

            foreach (TuningFrequency tuningFrequency in runParameters.FrequencyCollection)
            {
                if (tuningFrequency.TunerType == TunerType.Satellite && tuningFrequency.Provider != null)
                {
                    FillSatelliteDetails((SatelliteFrequency)tuningFrequency);
                    break;
                }
            }

            if (currentSatelliteFrequency != null)
                return;

            cboSatellite.SelectedIndex = 0;
            FillSatelliteDetails(currentSatelliteFrequency);
        }

        private void FillSatelliteDetails(SatelliteFrequency item)
        {
            currentFrequencyChanging = true;
            currentSatelliteFrequency = item;
            if (item != null)
            {
                cboSatellite.Text = item.Provider.Name;
                cboDVBSScanningFrequency.Text = item.ToString();
                cboDVBSCollectionType.Text = item.CollectionType.ToString();

                if (item.SatIpFrontend != -1)
                    udDvbsSatIpFrontend.SelectedItem = udDvbsSatIpFrontend.Items[item.SatIpFrontend].ToString();

                if (item.SatelliteDish == null)
                    item.SatelliteDish = SatelliteDish.FirstDefault;

                txtLNBLow.Text = item.SatelliteDish.LNBLowBandFrequency.ToString();
                txtLNBHigh.Text = item.SatelliteDish.LNBHighBandFrequency.ToString();
                txtLNBSwitch.Text = item.SatelliteDish.LNBSwitchFrequency.ToString();

                cboLNBType.Enabled = item.LNBConversion;
                cboLNBType.Text = item.SatelliteDish.LNBType.ToString();

                FillTunersList(TunerNodeType.Satellite, clbSatelliteTuners, item);

                if (cboDiseqc.Items.Count == 0)
                {
                    foreach (DiseqcSettings diseqcSetting in Enum.GetValues(typeof(DiseqcSettings)))
                        cboDiseqc.Items.Add(diseqcSetting);
                }

                if (item.DiseqcRunParamters.DiseqcSwitch != null)
                    cboDiseqc.Text = item.DiseqcRunParamters.DiseqcSwitch;
                else
                    cboDiseqc.SelectedIndex = 0;

                Collection<string> diseqcHandlers = DiseqcHandlerBase.Handlers;
                cboDiseqcHandler.Items.Clear();
                foreach (string diseqcHandler in diseqcHandlers)
                    cboDiseqcHandler.Items.Add(diseqcHandler);

                if (item.DiseqcRunParamters.DiseqcHandler != null)
                {
                    foreach (string diseqcHandler in cboDiseqcHandler.Items)
                    {
                        if (diseqcHandler.ToUpperInvariant() == item.DiseqcRunParamters.DiseqcHandler.ToUpperInvariant())
                            cboDiseqcHandler.SelectedItem = diseqcHandler;
                    }
                }
                else
                {
                    cboDiseqcHandler.SelectedItem = diseqcHandlers[0];
                    cboDiseqcHandler.Text = diseqcHandlers[0];
                }

                cbUseSafeDiseqc.Checked = OptionEntry.IsDefined(item.DiseqcRunParamters.Options, OptionName.UseSafeDiseqc);
                cbSwitchAfterPlay.Checked = OptionEntry.IsDefined(item.DiseqcRunParamters.Options, OptionName.SwitchAfterPlay);
                cbSwitchAfterTune.Checked = OptionEntry.IsDefined(item.DiseqcRunParamters.Options, OptionName.SwitchAfterTune);
                cbRepeatDiseqc.Checked = OptionEntry.IsDefined(item.DiseqcRunParamters.Options, OptionName.RepeatDiseqc);
                cbDisableDriverDiseqc.Checked = OptionEntry.IsDefined(item.DiseqcRunParamters.Options, OptionName.DisableDriverDiseqc);
                cbUseDiseqcCommands.Checked = OptionEntry.IsDefined(item.DiseqcRunParamters.Options, OptionName.UseDiseqcCommand);
            }

            currentFrequencyChanging = false;
        }

        private void InitializeTerrestrialTab()
        {
            foreach (Tuner tuner in Tuner.TunerCollection)
            {
                if (!tuner.Name.ToUpper().Contains("DVBLINK") && tuner.Supports(TunerNodeType.Terrestrial))
                    terrestrialUsed = true;
            }

            if (!terrestrialUsed)
            {
                tbcDeliverySystem.TabPages.RemoveByKey("tbpTerrestrial");
                return;
            }

            if (cboCountry.Items.Count == 0)
            {
                foreach (Country country in TerrestrialProvider.Countries)
                    cboCountry.Items.Add(country);
            }
            else
                cboCountry.SelectedItem = null;

            if (cboDVBTCollectionType.Items.Count == 0)
            {
                foreach (CollectionType collectionType in System.Enum.GetValues(typeof(CollectionType)))
                {
                    cboDVBTCollectionType.Items.Add(collectionType);
                }
            }

            udDvbtSatIpFrontend.Text = udDvbtSatIpFrontend.Items[0].ToString();

            foreach (TuningFrequency tuningFrequency in runParameters.FrequencyCollection)
            {
                if (tuningFrequency.TunerType == TunerType.Terrestrial && tuningFrequency.Provider != null)
                {
                    FillTerrestrialDetails((TerrestrialFrequency)tuningFrequency);
                    break;
                }
            }

            if (currentTerrestrialFrequency is null)
            {

                if (cboCountry.Items.Count > 0)
                {
                    cboCountry.SelectedIndex = 0;
                }

                FillTerrestrialDetails(currentTerrestrialFrequency);
            }
        }

        private void FillTerrestrialDetails(TerrestrialFrequency item)
        {
            currentFrequencyChanging = true;
            currentTerrestrialFrequency = item;

            if (item != null)
            {
                cboCountry.Text = ((TerrestrialProvider)item.Provider).Country.Name;
                cboArea.Text = ((TerrestrialProvider)item.Provider).Area.Name;
                cboDVBTScanningFrequency.Text = item.ToString();
                cboDVBTCollectionType.Text = item.CollectionType.ToString();

                if (item.SatIpFrontend != -1)
                    udDvbtSatIpFrontend.SelectedItem = udDvbtSatIpFrontend.Items[item.SatIpFrontend].ToString();

                FillTunersList(TunerNodeType.Terrestrial, clbTerrestrialTuners, item);
            }
            currentFrequencyChanging = false;
        }

        private void InitializeCableTab()
        {
            foreach (Tuner tuner in Tuner.TunerCollection)
            {
                if (!tuner.Name.ToUpper().Contains("DVBLINK") && tuner.Supports(TunerNodeType.Cable))
                    cableUsed = true;
            }

            if (!cableUsed)
            {
                tbcDeliverySystem.TabPages.RemoveByKey("tbpCable");
                return;
            }

            if (cboCable.Items.Count == 0)
            {
                foreach (CableProvider cableProvider in CableProvider.Providers)
                    cboCable.Items.Add(cableProvider);
            }
            else
                cboCable.SelectedItem = null;

            if (cboCableCollectionType.Items.Count == 0)
            {
                foreach (CollectionType collectionType in System.Enum.GetValues(typeof(CollectionType)))
                {
                    cboCableCollectionType.Items.Add(collectionType);
                }
            }

            udDvbcSatIpFrontend.Text = udDvbcSatIpFrontend.Items[0].ToString();

            foreach (var tuningFrequency in runParameters.FrequencyCollection)
            {
                if (tuningFrequency.TunerType == TunerType.Cable && tuningFrequency.Provider != null)
                {
                    FillCableDetails((CableFrequency)tuningFrequency);
                    break;
                }
            }

            if (currentCableFrequency is null)
            {
                if (cboCable.Items.Count > 0)
                {
                    cboCable.SelectedIndex = 0;
                }

                FillCableDetails(currentCableFrequency);
            }
        }

        private void FillCableDetails(CableFrequency item)
        {
            currentFrequencyChanging = true;
            currentCableFrequency = item;

            if (item != null)
            {
                cboCable.Text = item.Provider.Name;
                cboCableScanningFrequency.Text = item.ToString();
                cboCableCollectionType.Text = item.CollectionType.ToString();

                if (item.SatIpFrontend != -1)
                    udDvbcSatIpFrontend.SelectedItem = udDvbcSatIpFrontend.Items[item.SatIpFrontend].ToString();

                FillTunersList(TunerNodeType.Cable, clbCableTuners, item);
            }
            currentFrequencyChanging = false;
        }

        private void InitializeAtscTab()
        {
            foreach (Tuner tuner in Tuner.TunerCollection)
            {
                if (!tuner.Name.ToUpper().Contains("DVBLINK") && tuner.Supports(TunerNodeType.ATSC))
                    atscUsed = true;
            }

            if (!atscUsed)
            {
                tbcDeliverySystem.TabPages.RemoveByKey("tbpAtsc");
                return;
            }

            if (cboAtscProvider.Items.Count == 0)
            {
                foreach (AtscProvider atscProvider in AtscProvider.Providers)
                    cboAtscProvider.Items.Add(atscProvider);
            }
            else
                cboAtscProvider.SelectedItem = null;

            if (cboAtscCollectionType.Items.Count == 0)
            {
                foreach (CollectionType collectionType in System.Enum.GetValues(typeof(CollectionType)))
                    cboAtscCollectionType.Items.Add(collectionType);
            }

            foreach (TuningFrequency tuningFrequency in runParameters.FrequencyCollection)
            {
                if (tuningFrequency.TunerType == TunerType.ATSC || tuningFrequency.TunerType == TunerType.ATSCCable)
                {
                    if (tuningFrequency.Provider != null)
                    {
                        FillAtscDetails((AtscFrequency)tuningFrequency);
                        break;
                    }
                }
            }

            if (currentAtscFrequency != null)
                return;

            cboAtscProvider.SelectedIndex = 0;
            FillAtscDetails(currentAtscFrequency);
        }

        private void FillAtscDetails(AtscFrequency item)
        {
            currentFrequencyChanging = true;
            currentAtscFrequency = item;

            if (item != null)
            {
                cboAtscProvider.Text = item.Provider.Name;
                cboAtscScanningFrequency.Text = item.ToString();
                cboAtscCollectionType.Text = item.CollectionType.ToString();

                FillTunersList(TunerNodeType.ATSC, clbAtscTuners, item);
            }
            currentFrequencyChanging = false;
        }

        private void InitializeClearQamTab()
        {
            foreach (Tuner tuner in Tuner.TunerCollection)
            {
                if (!tuner.Name.ToUpper().Contains("DVBLINK") && tuner.Supports(TunerNodeType.Cable))
                    clearQamUsed = true;
            }

            if (!clearQamUsed)
            {
                tbcDeliverySystem.TabPages.RemoveByKey("tbpClearQAM");
                return;
            }

            if (cboClearQamProvider.Items.Count == 0)
            {
                foreach (ClearQamProvider clearQamProvider in ClearQamProvider.Providers)
                    cboClearQamProvider.Items.Add(clearQamProvider);
            }
            else
                cboClearQamProvider.SelectedItem = null;

            if (cboClearQamCollectionType.Items.Count == 0)
            {
                foreach (CollectionType collectionType in System.Enum.GetValues(typeof(CollectionType)))
                    cboClearQamCollectionType.Items.Add(collectionType);
            }

            foreach (TuningFrequency tuningFrequency in runParameters.FrequencyCollection)
            {
                if (tuningFrequency.TunerType == TunerType.ClearQAM && tuningFrequency.Provider != null)
                {
                    FillClearQamDetails((ClearQamFrequency)tuningFrequency);
                    break;
                }
            }

            if (currentClearQamFrequency is null)
            {
                if (cboClearQamProvider.Items.Count > 0)
                {
                    cboClearQamProvider.SelectedIndex = 0;
                }

                FillClearQamDetails(currentClearQamFrequency);
            }
        }

        private void FillClearQamDetails(ClearQamFrequency item)
        {
            currentFrequencyChanging = true;
            currentClearQamFrequency = item;

            if (item != null)
            {
                cboClearQamProvider.Text = item.Provider.Name;
                cboClearQamScanningFrequency.Text = item.ToString();
                cboClearQamCollectionType.Text = item.CollectionType.ToString();

                FillTunersList(TunerNodeType.Cable, clbClearQamTuners, item);
            }
            currentFrequencyChanging = false;
        }

        private void InitializeISDBSTab()
        {
            foreach (Tuner tuner in Tuner.TunerCollection)
            {
                if (!tuner.Name.ToUpper().Contains("DVBLINK") && tuner.Supports(TunerNodeType.ISDBS))
                    isdbsUsed = true;
            }

            if (!isdbsUsed)
            {
                tbcDeliverySystem.TabPages.RemoveByKey("tbpISDBSatellite");
                return;
            }

            if (cboISDBSatellite.Items.Count == 0)
            {
                foreach (ISDBSatelliteProvider provider in ISDBSatelliteProvider.Providers)
                    cboISDBSatellite.Items.Add(provider);
            }
            else
                cboISDBSatellite.SelectedItem = null;

            if (cboISDBSCollectionType.Items.Count == 0)
            {
                foreach (CollectionType collectionType in System.Enum.GetValues(typeof(CollectionType)))
                    cboISDBSCollectionType.Items.Add(collectionType);
            }

            foreach (TuningFrequency tuningFrequency in runParameters.FrequencyCollection)
            {
                if (tuningFrequency.TunerType == TunerType.ISDBS && tuningFrequency.Provider != null)
                {
                    FillISDBSatelliteDetails((ISDBSatelliteFrequency)tuningFrequency);
                    break;
                }
            }

            if (currentISDBSatelliteFrequency is null)
            {
                if (cboISDBSatellite.Items.Count > 0)
                {
                    cboISDBSatellite.SelectedIndex = 0;
                }

                FillISDBSatelliteDetails(currentISDBSatelliteFrequency);
            }
        }

        private void FillISDBSatelliteDetails(ISDBSatelliteFrequency item)
        {
            currentFrequencyChanging = true;
            currentISDBSatelliteFrequency = item;

            if (item != null)
            {
                cboISDBSatellite.Text = item.Provider.Name;
                cboISDBSScanningFrequency.Text = item.ToString();
                cboISDBSCollectionType.Text = item.CollectionType.ToString();

                if (item.SatelliteDish == null)
                    item.SatelliteDish = SatelliteDish.FirstDefault;

                txtISDBLNBLow.Text = item.SatelliteDish.LNBLowBandFrequency.ToString();
                txtISDBLNBHigh.Text = item.SatelliteDish.LNBHighBandFrequency.ToString();
                txtISDBLNBSwitch.Text = item.SatelliteDish.LNBSwitchFrequency.ToString();

                FillTunersList(TunerNodeType.ISDBS, clbISDBSatelliteTuners, item);
            }
            currentFrequencyChanging = false;
        }

        private void InitializeISDBTTab()
        {
            foreach (Tuner tuner in Tuner.TunerCollection)
            {
                if (!tuner.Name.ToUpper().Contains("DVBLINK") && tuner.Supports(TunerNodeType.ISDBT))
                    isdbtUsed = true;
            }

            if (!isdbtUsed)
            {
                tbcDeliverySystem.TabPages.RemoveByKey("tbpISDBTerrestrial");
                return;
            }

            if (cboISDBTProvider.Items.Count == 0)
            {
                foreach (ISDBTerrestrialProvider provider in ISDBTerrestrialProvider.Providers)
                    cboISDBTProvider.Items.Add(provider);
            }
            else
                cboISDBTProvider.SelectedItem = null;

            if (cboISDBTCollectionType.Items.Count == 0)
            {
                foreach (CollectionType collectionType in System.Enum.GetValues(typeof(CollectionType)))
                    cboISDBTCollectionType.Items.Add(collectionType);
            }

            foreach (TuningFrequency tuningFrequency in runParameters.FrequencyCollection)
            {
                if (tuningFrequency.TunerType == TunerType.ISDBT && tuningFrequency.Provider != null)
                {
                    FillISDBTerrestrialDetails((ISDBTerrestrialFrequency)tuningFrequency);
                    break;
                }
            }

            if (currentISDBTerrestrialFrequency is null)
            {
                if (cboISDBTProvider.Items.Count > 0)
                {
                    cboISDBTProvider.SelectedIndex = 0;
                }

                FillISDBTerrestrialDetails(currentISDBTerrestrialFrequency);
            }
        }

        private void FillISDBTerrestrialDetails(ISDBTerrestrialFrequency item)
        {
            if (item != null)
            {
                currentFrequencyChanging = true;
                currentISDBTerrestrialFrequency = item;

                cboISDBTProvider.Text = item.Provider.Name;
                cboISDBTScanningFrequency.Text = item.ToString();
                cboISDBTCollectionType.Text = item.CollectionType.ToString();

                FillTunersList(TunerNodeType.ISDBT, clbISDBTerrestrialTuners, item);

                currentFrequencyChanging = false;
            }
        }

        private void InitializeFileTab()
        {
            tbDeliveryFilePath.Text = null;

            if (cboDeliveryFileCollectionType.Items.Count == 0)
            {
                foreach (CollectionType collectionType in System.Enum.GetValues(typeof(CollectionType)))
                    cboDeliveryFileCollectionType.Items.Add(collectionType);
            }
            cboDeliveryFileCollectionType.SelectedIndex = 0;

            foreach (TuningFrequency tuningFrequency in runParameters.FrequencyCollection)
            {
                if (tuningFrequency.TunerType == TunerType.File)
                {
                    FillFileDetails((FileFrequency)tuningFrequency);
                    break;
                }
            }

            if (currentFileFrequency is null)
            {
                FillFileDetails(currentFileFrequency);
            }
        }

        private void FillFileDetails(FileFrequency item)
        {
            currentFrequencyChanging = true;
            currentFileFrequency = item;

            if (item != null)
            {
                tbDeliveryFilePath.Text = item.Path;
                cboDeliveryFileCollectionType.Text = item.CollectionType.ToString();
            }
            else
            {
                tbDeliveryFilePath.Text = null;
                cboDeliveryFileCollectionType.SelectedIndex = 0;
            }

            currentFrequencyChanging = false;
        }

        private void InitializeStreamTab()
        {
            tbStreamIpAddress.Text = null;
            nudStreamPortNumber.Value = 5004;
            cboStreamProtocol.Text = cboStreamProtocol.Items[0].ToString();

            if (cboStreamCollectionType.Items.Count == 0)
            {
                foreach (CollectionType collectionType in System.Enum.GetValues(typeof(CollectionType)))
                    cboStreamCollectionType.Items.Add(collectionType);
            }
            cboStreamCollectionType.SelectedIndex = 0;

            tbStreamPath.Text = null;

            foreach (TuningFrequency tuningFrequency in runParameters.FrequencyCollection)
            {
                if (tuningFrequency.TunerType == TunerType.Stream)
                {
                    FillStreamDetails((StreamFrequency)tuningFrequency);
                    break;
                }
            }

            if (currentStreamFrequency is null)
            {
                FillStreamDetails(currentStreamFrequency);
            }
        }

        private void FillStreamDetails(StreamFrequency item)
        {
            currentFrequencyChanging = true;
            currentStreamFrequency = item;

            if (item != null)
            {
                if (item.IPAddress != "0.0.0.0")
                    tbStreamIpAddress.Text = item.IPAddress;
                else
                    tbStreamIpAddress.Text = string.Empty;
                nudStreamPortNumber.Value = item.PortNumber;

                if (!string.IsNullOrWhiteSpace(item.MulticastSource))
                {
                    tbStreamMulticastSourceIP.Text = item.MulticastSource;
                    nudStreamMulticastSourcePort.Value = item.MulticastPort;
                }
                else
                {
                    tbStreamMulticastSourceIP.Text = null;
                    nudStreamMulticastSourcePort.Value = 0;
                }

                cboStreamProtocol.Text = item.Protocol.ToString();
                tbStreamPath.Text = item.Path;
                cboStreamCollectionType.Text = item.CollectionType.ToString();
            }
            else
            {
                tbStreamIpAddress.Text = null;
                nudStreamPortNumber.Value = 80;
                tbStreamMulticastSourceIP.Text = null;
                nudStreamMulticastSourcePort.Value = 0;
                cboStreamProtocol.SelectedIndex = 0;
                tbStreamPath.Text = null;
                cboStreamCollectionType.SelectedIndex = 0;
            }

            currentFrequencyChanging = false;
        }

        private void InitializeOutputTab()
        {
            cbAllowBreaks.Checked = OptionEntry.IsDefined(runParameters.Options, OptionName.AcceptBreaks);
            cbRoundTime.Checked = OptionEntry.IsDefined(runParameters.Options, OptionName.RoundTime);
            cbRemoveExtractedData.Checked = OptionEntry.IsDefined(runParameters.Options, OptionName.NoRemoveData);
            cbCreateSameData.Checked = OptionEntry.IsDefined(runParameters.Options, OptionName.DuplicateSameChannels);
            cbNoLogExcluded.Checked = OptionEntry.IsDefined(runParameters.Options, OptionName.NoLogExcluded);
            cbTcRelevantChannels.Checked = OptionEntry.IsDefined(runParameters.Options, OptionName.TcRelevantOnly);
            cbAddSeasonEpisodeToDesc.Checked = OptionEntry.IsDefined(runParameters.Options, OptionName.AddSeasonEpisodeToDesc);
            cbNoDataNoFile.Checked = OptionEntry.IsDefined(runParameters.Options, OptionName.NoDataNoFile);

            if (runParameters.OutputFileSet)
            {
                cbXmltvOutputEnabled.Checked = true;

                txtOutputFile.Text = runParameters.OutputFileName;

                if (OptionEntry.IsDefined(runParameters.Options, OptionName.ChannelIdSeqNo))
                    cboChannelIDFormat.SelectedItem = cboChannelIDFormat.Items[2];
                else
                {
                    if (OptionEntry.IsDefined(runParameters.Options, OptionName.ChannelIdFullName))
                        cboChannelIDFormat.SelectedItem = cboChannelIDFormat.Items[3];
                    else
                    {
                        if (OptionEntry.IsDefined(runParameters.Options, OptionName.UseChannelId))
                            cboChannelIDFormat.SelectedItem = cboChannelIDFormat.Items[1];
                        else
                            cboChannelIDFormat.SelectedItem = cboChannelIDFormat.Items[0];
                    }
                }

                if (OptionEntry.IsDefined(runParameters.Options, OptionName.UseNumericCrid))
                    cboEpisodeTagFormat.SelectedIndex = 3;
                else
                {
                    if (OptionEntry.IsDefined(runParameters.Options, OptionName.UseRawCrid))
                        cboEpisodeTagFormat.SelectedIndex = 2;
                    else
                    {
                        if (OptionEntry.IsDefined(runParameters.Options, OptionName.UseBsepg))
                            cboEpisodeTagFormat.SelectedIndex = 1;
                        else
                        {
                            if (OptionEntry.IsDefined(runParameters.Options, OptionName.ValidEpisodeTag))
                                cboEpisodeTagFormat.SelectedIndex = 0;
                            else
                            {
                                if (OptionEntry.IsDefined(runParameters.Options, OptionName.VBoxEpisodeTag))
                                    cboEpisodeTagFormat.SelectedIndex = 4;
                                else
                                {
                                    if (OptionEntry.IsDefined(runParameters.Options, OptionName.NoEpisodeTag))
                                        cboEpisodeTagFormat.SelectedIndex = 5;
                                    else
                                        cboEpisodeTagFormat.SelectedIndex = 0;
                                }
                            }
                        }
                    }
                }

                cbUseLCN.Checked = OptionEntry.IsDefined(runParameters.Options, OptionName.UseLcn);
                cbCreateADTag.Checked = OptionEntry.IsDefined(runParameters.Options, OptionName.CreateAdTag);
                cbElementPerTag.Checked = OptionEntry.IsDefined(runParameters.Options, OptionName.ElementPerTag);
                cbOmitPartNumber.Checked = OptionEntry.IsDefined(runParameters.Options, OptionName.OmitPartNumber);
                cbPrefixDescWithAirDate.Checked = OptionEntry.IsDefined(runParameters.Options, OptionName.PrefixDescriptionWithAirDate);
                cbPrefixSubtitleWithSeasonEpisode.Checked = OptionEntry.IsDefined(runParameters.Options, OptionName.PrefixSubtitleWithSeasonEpisode);
                cbCreatePlexEpisodeNumTag.Checked = OptionEntry.IsDefined(runParameters.Options, OptionName.CreatePlexEpisodeNumTag);

                tbChannelLogoPath.Text = runParameters.ChannelLogoPath;
            }
            else
            {
                cbXmltvOutputEnabled.Checked = false;
                ResetXmltvOptions();
            }

            if (Environment.OSVersion.Version.Major != 5 || CommandLine.WmcPresent)
            {
                cbWmcOutputEnabled.Checked = OptionEntry.IsDefined(runParameters.Options, OptionName.WmcImport);

                if (cbWmcOutputEnabled.Checked)
                {
                    txtImportName.Text = runParameters.WMCImportName;
                    cbAutoMapEPG.Checked = OptionEntry.IsDefined(runParameters.Options, OptionName.AutoMapEpg);
                    cbWMCFourStarSpecial.Checked = OptionEntry.IsDefined(runParameters.Options, OptionName.WmcStarSpecial);
                    cbDisableInbandLoader.Checked = OptionEntry.IsDefined(runParameters.Options, OptionName.DisableInbandLoader);

                    if (OptionEntry.IsDefined(runParameters.Options, OptionName.UseWmcRepeatCheck))
                        cboWMCSeries.SelectedIndex = 1;
                    else
                    {
                        if (OptionEntry.IsDefined(runParameters.Options, OptionName.UseWmcRepeatCheckBroadcast))
                            cboWMCSeries.SelectedIndex = 2;
                        else
                            cboWMCSeries.SelectedIndex = 0;
                    }
                }
                else
                {
                    cbWmcOutputEnabled.Checked = false;
                    ResetWMCOptions();
                }
            }
            else
            {
                cbWmcOutputEnabled.Checked = false;
                ResetWMCOptions();
            }

            txtImportName.KeyPress -= new KeyPressEventHandler(txtImportName_KeyPressAlphaNumeric);
            txtImportName.KeyPress += new KeyPressEventHandler(txtImportName_KeyPressAlphaNumeric);

            if (OptionEntry.IsDefined(runParameters.Options, OptionName.UseDvbViewer) ||
                runParameters.ImportingToDvbViewer)
            {
                cbDvbViewerOutputEnabled.Checked = true;

                cbUseDVBViewer.Checked = OptionEntry.IsDefined(runParameters.Options, OptionName.UseDvbViewer);
                cbDVBViewerImport.Checked = OptionEntry.IsDefined(runParameters.Options, OptionName.DvbViewerImport);
                cbRecordingServiceImport.Checked = OptionEntry.IsDefined(runParameters.Options, OptionName.DvbViewerRecSvcImport);
                cbDVBViewerSubtitleVisible.Checked = OptionEntry.IsDefined(runParameters.Options, OptionName.DvbViewerSubtitleVisible);
                cbDVBViewerClear.Checked = OptionEntry.IsDefined(runParameters.Options, OptionName.DvbViewerClear);
                tbDVBViewerIPAddress.Text = runParameters.DVBViewerIPAddress;

                decimal port = 8089;

                OptionEntry optionEntry = OptionEntry.FindEntry(runParameters.Options, OptionName.DvbViewerRecSvcImport, true);
                if (optionEntry != null)
                    port = optionEntry.Parameter;

                nudPort.Value = port;
            }
            else
            {
                cbDvbViewerOutputEnabled.Checked = false;
                ResetDVBViewerOptions();
            }
        }

        private void ResetXmltvOptions()
        {
            gpXmltvOptions.Enabled = false;

            txtOutputFile.Text = null;
            cboChannelIDFormat.SelectedItem = cboChannelIDFormat.Items[0];
            cboEpisodeTagFormat.SelectedItem = cboEpisodeTagFormat.Items[0];
            cbUseLCN.Checked = false;
            cbCreateADTag.Checked = false;
            cbElementPerTag.Checked = false;
            cbOmitPartNumber.Checked = false;

            tbChannelLogoPath.Text = null;
        }

        private void ResetWMCOptions()
        {
            gpWMCOptions.Enabled = false;

            txtImportName.Text = string.Empty;
            cbAutoMapEPG.Checked = false;
            cbWMCFourStarSpecial.Checked = false;
            cbDisableInbandLoader.Checked = false;
            cboWMCSeries.SelectedIndex = 0;
        }

        private void ResetDVBViewerOptions()
        {
            gpDVBViewerOptions.Enabled = false;

            cbUseDVBViewer.Checked = false;
            cbDVBViewerImport.Checked = false;
            cbRecordingServiceImport.Checked = false;
            cbDVBViewerSubtitleVisible.Checked = false;
            cbDVBViewerClear.Checked = false;
        }

        private void InitializeFilesTab()
        {
            cbBladeRunnerFile.Checked = OptionEntry.IsDefined(runParameters.Options, OptionName.CreateBrChannels);
            gpBladeRunnerFile.Enabled = cbBladeRunnerFile.Checked;
            if (cbBladeRunnerFile.Checked)
                tbBladeRunnerFileName.Text = runParameters.BladeRunnerFileName;
            else
                tbBladeRunnerFileName.Text = null;

            cbAreaRegionFile.Checked = OptionEntry.IsDefined(runParameters.Options, OptionName.CreateArChannels);
            gpAreaRegionFile.Enabled = cbAreaRegionFile.Checked;
            if (cbAreaRegionFile.Checked)
                tbAreaRegionFileName.Text = runParameters.AreaRegionFileName;
            else
                tbAreaRegionFileName.Text = null;

            cbSageTVFile.Checked = OptionEntry.IsDefined(runParameters.Options, OptionName.CreateSageTvFrq);
            gpSageTVFile.Enabled = cbSageTVFile.Checked;
            if (cbSageTVFile.Checked)
                tbSageTVFileName.Text = runParameters.SageTVFileName;
            else
                tbSageTVFileName.Text = null;

            cbSageTVFileNoEPG.Checked = OptionEntry.IsDefined(runParameters.Options, OptionName.SageTvOmitNoEpg);

            if (runParameters.SageTVSatelliteNumber != -1)
                tbSageTVSatelliteNumber.Text = runParameters.SageTVSatelliteNumber.ToString();
            else
                tbSageTVSatelliteNumber.Text = string.Empty;
        }

        private void InitializeChannelsTab()
        {
            if (runParameters.StationCollection.Count != 0)
            {
                Collection<TVStation> sortedStations = new Collection<TVStation>();

                foreach (TVStation station in runParameters.StationCollection)
                    addInOrder(sortedStations, station, true, "Name");

                bindingList = new BindingList<TVStation>();
                foreach (TVStation station in sortedStations)
                    bindingList.Add(station);

                tvStationBindingSource.DataSource = bindingList;
            }
            else
                tvStationBindingSource.DataSource = null;

            sortedColumnName = "nameColumn";
            sortedKeyName = "Name";
            sortedAscending = true;
        }

        private void InitializeTimeShiftTab()
        {
            populatePlusChannels(lbPlusSourceChannel);
            populatePlusChannels(lbPlusDestinationChannel);
            lvPlusSelectedChannels.Items.Clear();

            foreach (TimeOffsetChannel timeOffsetChannel in runParameters.TimeOffsetChannels)
            {
                ListViewItem newItem = new ListViewItem(timeOffsetChannel.SourceChannel.Name);
                newItem.Tag = timeOffsetChannel;
                newItem.SubItems.Add(timeOffsetChannel.DestinationChannel.Name);
                newItem.SubItems.Add(timeOffsetChannel.Offset.ToString());
                lvPlusSelectedChannels.Items.Add(newItem);
            }

            btPlusDelete.Enabled = (lvPlusSelectedChannels.SelectedItems.Count != 0);
        }

        private void InitializeFiltersTab()
        {
            cboFilterFrequency.Items.Clear();

            foreach (TuningFrequency tuningFrequency in runParameters.FrequencyCollection)
            {
                if (tuningFrequency.TunerType != TunerType.File && tuningFrequency.TunerType != TunerType.Stream)
                    cboFilterFrequency.Items.Add(tuningFrequency);
            }

            if (cboFilterFrequency.Items.Count != 0)
                cboFilterFrequency.SelectedIndex = 0;

            lvExcludedIdentifiers.Items.Clear();

            foreach (ChannelFilterEntry filterEntry in runParameters.ChannelFilters)
            {
                ListViewItem newItem = null;

                if (filterEntry.Frequency != -1)
                    newItem = new ListViewItem(filterEntry.Frequency.ToString());
                else
                    newItem = new ListViewItem(string.Empty);

                newItem.Tag = filterEntry;

                if (filterEntry.OriginalNetworkID != -1)
                    newItem.SubItems.Add(filterEntry.OriginalNetworkID.ToString());
                else
                    newItem.SubItems.Add("");

                if (filterEntry.TransportStreamID != -1)
                    newItem.SubItems.Add(filterEntry.TransportStreamID.ToString());
                else
                    newItem.SubItems.Add("");

                if (filterEntry.StartServiceID != -1)
                    newItem.SubItems.Add(filterEntry.StartServiceID.ToString());
                else
                    newItem.SubItems.Add("");

                if (filterEntry.EndServiceID != -1)
                    newItem.SubItems.Add(filterEntry.EndServiceID.ToString());
                else
                    newItem.SubItems.Add("");

                lvExcludedIdentifiers.Items.Add(newItem);
            }

            if (runParameters.MaxService != -1)
                tbExcludedMaxChannel.Text = runParameters.MaxService.ToString();
            else
                tbExcludedMaxChannel.Text = string.Empty;
        }

        private void InitializeRepeatsTab()
        {
            tbRepeatTitle.Text = string.Empty;
            tbRepeatDescription.Text = string.Empty;
            lvRepeatPrograms.Items.Clear();
            tbPhrasesToIgnore.Text = string.Empty;

            cbCheckForRepeats.Checked = OptionEntry.IsDefined(runParameters.Options, OptionName.CheckForRepeats);

            if (!cbCheckForRepeats.Checked)
            {
                cbNoSimulcastRepeats.Checked = false;
                cbNoSimulcastRepeats.Enabled = false;
                cbIgnoreWMCRecordings.Checked = false;
                cbIgnoreWMCRecordings.Enabled = false;
                gpRepeatExclusions.Enabled = false;
                return;
            }

            cbNoSimulcastRepeats.Checked = OptionEntry.IsDefined(runParameters.Options, OptionName.NoSimulcastRepeats);
            cbNoSimulcastRepeats.Enabled = true;
            cbIgnoreWMCRecordings.Checked = OptionEntry.IsDefined(runParameters.Options, OptionName.IgnoreWmcRecordings);
            cbIgnoreWMCRecordings.Enabled = true;

            foreach (RepeatExclusion repeatExclusion in runParameters.Exclusions)
            {
                ListViewItem newItem = new ListViewItem(repeatExclusion.Title);
                newItem.SubItems.Add(repeatExclusion.Description);
                lvRepeatPrograms.Items.Add(newItem);
            }

            StringBuilder phrasesToIgnore = new StringBuilder();
            foreach (string phrase in runParameters.PhrasesToIgnore)
            {
                if (phrasesToIgnore.Length != 0)
                    phrasesToIgnore.Append(',');
                phrasesToIgnore.Append(phrase);
            }

            tbPhrasesToIgnore.Text = phrasesToIgnore.ToString();
        }

        private void InitializeAdvancedTab()
        {
            cbStoreStationInfo.Checked = OptionEntry.IsDefined(runParameters.Options, OptionName.StoreStationInfo);
            cbUseStoredStationInfo.Checked = OptionEntry.IsDefined(runParameters.Options, OptionName.UseStoredStationInfo);
            cbFromService.Checked = OptionEntry.IsDefined(runParameters.Options, OptionName.RunFromService);

            nudSignalLockTimeout.Value = (decimal)runParameters.LockTimeout.TotalSeconds;
            nudDataCollectionTimeout.Value = (decimal)runParameters.FrequencyTimeout.TotalSeconds;
            nudScanRetries.Value = (decimal)runParameters.Repeats;
            nudBufferSize.Value = (decimal)runParameters.BufferSize;
            nudBufferFills.Value = (decimal)runParameters.BufferFills;

            cbManualTime.Checked = runParameters.TimeZoneSet;
            gpTimeAdjustments.Enabled = runParameters.TimeZoneSet;

            if (cbManualTime.Checked)
            {
                nudCurrentOffsetHours.Value = runParameters.TimeZone.Hours;
                nudCurrentOffsetMinutes.Value = runParameters.TimeZone.Minutes;
                nudNextOffsetHours.Value = runParameters.NextTimeZone.Hours;
                nudNextOffsetMinutes.Value = runParameters.NextTimeZone.Minutes;
                tbChangeDate.Text = runParameters.NextTimeZoneChange.Date.ToString("ddMMyy");
                nudChangeHours.Value = runParameters.NextTimeZoneChange.Hour;
                nudChangeMinutes.Value = runParameters.NextTimeZoneChange.Minute;
            }
            else
            {
                nudCurrentOffsetHours.Value = 0;
                nudCurrentOffsetMinutes.Value = 0;
                nudNextOffsetHours.Value = 0;
                nudNextOffsetMinutes.Value = 0;
                tbChangeDate.Text = string.Empty;
                nudChangeHours.Value = 0;
                nudChangeMinutes.Value = 0;
            }
        }

        private void InitializeLookupTab()
        {
            cbMovieLookupEnabled.Checked = runParameters.MovieLookupEnabled;
            if (runParameters.DownloadMovieThumbnail == LookupImageType.Thumbnail)
                cboxMovieLookupImageType.SelectedIndex = 0;
            else
            {
                if (runParameters.DownloadMovieThumbnail == LookupImageType.Poster)
                    cboxMovieLookupImageType.SelectedIndex = 1;
                else
                    cboxMovieLookupImageType.SelectedIndex = 2;
            }
            nudLookupMovieLowDuration.Value = runParameters.MovieLowTime;
            nudLookupMovieHighDuration.Value = runParameters.MovieHighTime;

            tbLookupMoviePhrases.Text = string.Empty;
            foreach (string lookupPhrase in runParameters.LookupMoviePhrases)
            {
                if (tbLookupMoviePhrases.Text.Length != 0)
                    tbLookupMoviePhrases.Text = tbLookupMoviePhrases.Text + runParameters.MoviePhraseSeparator + lookupPhrase;
                else
                    tbLookupMoviePhrases.Text = tbLookupMoviePhrases.Text + lookupPhrase;
            }

            cboLookupNotMovie.Items.Clear();
            if (runParameters.LookupNotMovie != null)
            {
                foreach (string notMovie in runParameters.LookupNotMovie)
                    cboLookupNotMovie.Items.Add(notMovie);

                if (cboLookupNotMovie.Items.Count > 0)
                    cboLookupNotMovie.SelectedIndex = 0;
            }

            gpMovieLookup.Enabled = runParameters.MovieLookupEnabled;

            cbTVLookupEnabled.Checked = runParameters.TVLookupEnabled;
            switch (runParameters.DownloadTVThumbnail)
            {
                case LookupImageType.Poster:
                    cboxTVLookupImageType.SelectedIndex = 0;
                    break;
                case LookupImageType.Banner:
                    cboxTVLookupImageType.SelectedIndex = 1;
                    break;
                case LookupImageType.Fanart:
                    cboxTVLookupImageType.SelectedIndex = 2;
                    break;
                case LookupImageType.SmallPoster:
                    cboxTVLookupImageType.SelectedIndex = 3;
                    break;
                case LookupImageType.SmallFanart:
                    cboxTVLookupImageType.SelectedIndex = 4;
                    break;
                case LookupImageType.None:
                    cboxTVLookupImageType.SelectedIndex = 5;
                    break;
                default:
                    cboxTVLookupImageType.SelectedIndex = 5;
                    break;
            }
            gpTVLookup.Enabled = runParameters.TVLookupEnabled;

            gpLookupMisc.Enabled = gpMovieLookup.Enabled || gpTVLookup.Enabled;

            cbxLookupMatching.Text = runParameters.LookupMatching.ToString();
            nudLookupMatchThreshold.Value = runParameters.LookupMatchThreshold;

            cbLookupNotFound.Checked = runParameters.LookupNotFound;
            cbLookupReload.Checked = runParameters.LookupReload;
            cbLookupIgnoreCategories.Checked = runParameters.LookupIgnoreCategories;
            cbLookupProcessAsTVSeries.Checked = runParameters.LookupProcessAsTVSeries;
            nudLookupTime.Value = runParameters.LookupTimeLimit;
            nudLookupErrors.Value = runParameters.LookupErrorLimit;

            tbLookupIgnoredPhrases.Text = string.Empty;
            foreach (string lookupPhrase in runParameters.LookupIgnoredPhrases)
            {
                if (tbLookupIgnoredPhrases.Text.Length != 0)
                    tbLookupIgnoredPhrases.Text = tbLookupIgnoredPhrases.Text + runParameters.LookupIgnoredPhraseSeparator + lookupPhrase;
                else
                    tbLookupIgnoredPhrases.Text = tbLookupIgnoredPhrases.Text + lookupPhrase;
            }

            tbLookupImagePath.Text = runParameters.LookupImagePath;
            tbLookupXmltvImageTagPath.Text = runParameters.LookupXmltvImageTagPath;

            udIgnorePhraseSeparator.Text = runParameters.LookupIgnoredPhraseSeparator;
            udMoviePhraseSeparator.Text = runParameters.MoviePhraseSeparator;

            cbLookupImagesInBase.Checked = runParameters.LookupImagesInBase;
            cbLookupImageNameTitle.Checked = runParameters.LookupImageNameTitle;
        }

        private void InitializeDiagnosticsTab()
        {
            tbDebugIDs.Text = string.Empty;
            foreach (DebugEntry debugEntry in runParameters.DebugIDs)
            {
                if (tbDebugIDs.Text.Length != 0)
                    tbDebugIDs.Text = tbDebugIDs.Text + "," + debugEntry;
                else
                    tbDebugIDs.Text = tbDebugIDs.Text + debugEntry;
            }

            tbTraceIDs.Text = string.Empty;
            foreach (TraceEntry traceEntry in runParameters.TraceIDs)
            {
                if (tbTraceIDs.Text.Length != 0)
                    tbTraceIDs.Text = tbTraceIDs.Text + "," + traceEntry;
                else
                    tbTraceIDs.Text = tbTraceIDs.Text + traceEntry;
            }
        }

        private void InitializeUpdateTab()
        {
            gpDVBLink.Enabled = runParameters.ChannelUpdateEnabled;

            cbDVBLinkUpdateEnabled.Checked = runParameters.ChannelUpdateEnabled;

            switch (runParameters.ChannelMergeMethod)
            {
                case ChannelMergeMethod.None:
                    cboMergeMethod.SelectedIndex = 0;
                    break;
                case ChannelMergeMethod.Name:
                    cboMergeMethod.SelectedIndex = 1;
                    break;
                case ChannelMergeMethod.Number:
                    cboMergeMethod.SelectedIndex = 2;
                    break;
                case ChannelMergeMethod.NameNumber:
                    cboMergeMethod.SelectedIndex = 3;
                    break;
                default:
                    cboMergeMethod.SelectedIndex = 0;
                    break;
            }
            cboMergeMethod.Text = cboMergeMethod.Items[cboMergeMethod.SelectedIndex].ToString();

            switch (runParameters.ChannelEPGScanner)
            {
                case ChannelEPGScanner.EITScanner:
                    cboEPGScanner.SelectedIndex = 3;
                    break;
                case ChannelEPGScanner.EPGCollector:
                    cboEPGScanner.SelectedIndex = 2;
                    break;
                case ChannelEPGScanner.None:
                    cboEPGScanner.SelectedIndex = 0;
                    break;
                case ChannelEPGScanner.Default:
                    cboEPGScanner.SelectedIndex = 1;
                    break;
                case ChannelEPGScanner.Xmltv:
                    cboEPGScanner.SelectedIndex = 4;
                    break;
                default:
                    cboEPGScanner.SelectedIndex = 0;
                    break;
            }
            cboEPGScanner.Text = cboEPGScanner.Items[cboEPGScanner.SelectedIndex].ToString();

            cbChildLock.Checked = runParameters.ChannelChildLock;
            nudEPGScanInterval.Value = runParameters.ChannelEPGScanInterval;
            cbLogNetworkMap.Checked = runParameters.ChannelLogNetworkMap;
            cbReloadChannelData.Checked = runParameters.ChannelReloadData;
            cbUpdateChannelNumbers.Checked = runParameters.ChannelUpdateNumber;
            cbAutoExcludeNew.Checked = runParameters.ChannelExcludeNew;
        }

        private void InitializeXmltvTab()
        {
            btXmltvDelete.Enabled = false;
            btXmltvLoadFiles.Enabled = false;
            btXmltvClear.Enabled = false;
            btXmltvIncludeAll.Enabled = false;
            btXmltvExcludeAll.Enabled = false;

            if (cboXmltvLanguage.Items.Count == 0)
            {
                foreach (LanguageCode languageCode in LanguageCode.LanguageCodeList)
                    cboXmltvLanguage.Items.Add(languageCode);
            }
            cboXmltvLanguage.SelectedIndex = 0;

            if (cboXmltvTimeZone.Items.Count == 0)
            {
                cboXmltvTimeZone.Items.Add(" -- Local --");

                foreach (TimeZoneInfo timeZoneInfo in TimeZoneInfo.GetSystemTimeZones())
                    cboXmltvTimeZone.Items.Add(timeZoneInfo.Id);
            }

            cboXmltvPrecedence.SelectedIndex = 0;
            cboXmltvIdFormat.SelectedIndex = 0;
            cboXmltvTimeZone.SelectedIndex = 0;

            lvXmltvSelectedFiles.Items.Clear();

            if (runParameters.ImportFiles != null)
            {
                foreach (ImportFileSpec importFileSpec in runParameters.ImportFiles)
                {
                    ListViewItem item = new ListViewItem(importFileSpec.FileName);
                    item.Tag = importFileSpec;

                    if (importFileSpec.Language != null)
                        item.SubItems.Add(importFileSpec.Language.Description);
                    else
                        item.SubItems.Add(string.Empty);

                    item.SubItems.Add(importFileSpec.Precedence.ToString());
                    item.SubItems.Add(importFileSpec.NoLookup ? "Yes" : "No");
                    item.SubItems.Add(importFileSpec.AppendOnly ? "Yes" : "No");
                    item.SubItems.Add(string.IsNullOrWhiteSpace(importFileSpec.TimeZone) ? "Local" : importFileSpec.TimeZone);

                    lvXmltvSelectedFiles.Items.Add(item);
                }

                btXmltvDelete.Enabled = false;
                btXmltvLoadFiles.Enabled = lvXmltvSelectedFiles.Items.Count != 0;
            }

            if (runParameters.ImportChannelChanges != null)
            {
                xmltvChannelBindingList = new BindingList<ImportChannelChange>();
                foreach (ImportChannelChange channelChange in runParameters.ImportChannelChanges)
                    addChannelChange(xmltvChannelBindingList, channelChange);

                xmltvChannelChangeBindingSource.DataSource = xmltvChannelBindingList;
                dgXmltvChannelChanges.DataSource = xmltvChannelChangeBindingSource;

                btXmltvClear.Enabled = true;
                btXmltvIncludeAll.Enabled = true;
                btXmltvExcludeAll.Enabled = true;
            }
            else
            {
                dgXmltvChannelChanges.DataSource = null;
                xmltvChannelBindingList = null;
            }
        }

        private void InitializeEditTab()
        {
            btEditDelete.Enabled = false;

            tbEditText.Text = null;
            tbEditReplacementText.Text = null;
            cboEditLocation.SelectedIndex = 0;
            cboEditApplyTo.SelectedIndex = 2;
            cboEditReplaceMode.SelectedIndex = 0;

            lvEditSpecs.Items.Clear();

            if (runParameters.EditSpecs != null)
            {
                foreach (EditSpec editSpec in runParameters.EditSpecs)
                {
                    ListViewItem item = new ListViewItem(editSpec.Text);
                    item.Tag = editSpec;

                    if (editSpec.ApplyToTitles)
                    {
                        if (editSpec.ApplyToDescriptions)
                            item.SubItems.Add("Titles and descriptions");
                        else
                            item.SubItems.Add("Titles only");
                    }
                    else
                    {
                        if (editSpec.ApplyToDescriptions)
                            item.SubItems.Add("Descriptions only");
                    }

                    item.SubItems.Add(editSpec.Location.ToString());

                    if (editSpec.ReplacementText != null)
                        item.SubItems.Add(editSpec.ReplacementText);
                    else
                        item.SubItems.Add(string.Empty);

                    switch (editSpec.ReplacementMode)
                    {
                        case TextReplacementMode.TextOnly:
                            item.SubItems.Add("Text only");
                            break;
                        case TextReplacementMode.TextAndFollowing:
                            item.SubItems.Add("Text and following");
                            break;
                        case TextReplacementMode.TextAndPreceeding:
                            item.SubItems.Add("Text and preceeding");
                            break;
                        case TextReplacementMode.Everything:
                            item.SubItems.Add("Everything");
                            break;
                        default:
                            item.SubItems.Add("Text only");
                            break;
                    }

                    lvEditSpecs.Items.Add(item);
                }

                btEditDelete.Enabled = false;
            }
        }

        private void txtLNBNumeric(object sender, KeyPressEventArgs e)
        {
            if ("0123456789\b".IndexOf(e.KeyChar) == -1)
                e.Handled = true;
        }

        private void txtLNBLow_TextChanged(object sender, EventArgs e)
        {
            btAddSatellite.Enabled = txtLNBLow.Text.Length != 0 && txtLNBHigh.Text.Length != 0 && txtLNBSwitch.Text.Length != 0;
            txtLNBLow.BackColor = txtLNBLow.Text.Length != 0 ? Color.White : Color.Red;
        }

        private void txtLNBHigh_TextChanged(object sender, EventArgs e)
        {
            btAddSatellite.Enabled = txtLNBLow.Text.Length != 0 && txtLNBHigh.Text.Length != 0 && txtLNBSwitch.Text.Length != 0;
            txtLNBHigh.BackColor = txtLNBHigh.Text.Length != 0 ? Color.White : Color.Red;
        }

        private void txtLNBSwitch_TextChanged(object sender, EventArgs e)
        {
            btAddSatellite.Enabled = txtLNBLow.Text.Length != 0 && txtLNBHigh.Text.Length != 0 && txtLNBSwitch.Text.Length != 0;
            txtLNBSwitch.BackColor = txtLNBSwitch.Text.Length != 0 ? Color.White : Color.Red;
        }

        private void btLNBDefaults_Click(object sender, EventArgs e)
        {
            SatelliteDish defaultSatellite = SatelliteDish.Default;

            txtLNBLow.Text = defaultSatellite.LNBLowBandFrequency.ToString();
            txtLNBHigh.Text = defaultSatellite.LNBHighBandFrequency.ToString();
            txtLNBSwitch.Text = defaultSatellite.LNBSwitchFrequency.ToString();

            cboLNBType.SelectedIndex = 0;
        }

        private void cboSatellite_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cboSatellite.SelectedItem != null)
            {
                cboDVBSScanningFrequency.Items.Clear();
                foreach (TuningFrequency tuningFrequency in ((Satellite)cboSatellite.SelectedItem).Frequencies)
                    cboDVBSScanningFrequency.Items.Add(tuningFrequency);
                cboDVBSScanningFrequency.SelectedIndex = 0;

                if (cboDiseqc.Items.Count > 0)
                    cboDiseqc.SelectedIndex = 0;
                if (cboDiseqcHandler.Items.Count > 0)
                    cboDiseqcHandler.SelectedIndex = 0;
                cboDiseqcHandler.Enabled = false;
                cbUseSafeDiseqc.Checked = false;
                cbUseSafeDiseqc.Enabled = false;
                cbSwitchAfterPlay.Checked = false;
                cbSwitchAfterPlay.Enabled = false;
                cbSwitchAfterTune.Checked = false;
                cbSwitchAfterTune.Enabled = false;
                cbRepeatDiseqc.Checked = false;
                cbRepeatDiseqc.Enabled = false;
                cbDisableDriverDiseqc.Checked = false;
                cbDisableDriverDiseqc.Enabled = false;
                cbUseDiseqcCommands.Checked = false;
                cbUseDiseqcCommands.Enabled = false;
            }
        }

        private void cboDVBSScanningFrequency_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!currentFrequencyChanging)
                currentSatelliteFrequency = (SatelliteFrequency)((SatelliteFrequency)cboDVBSScanningFrequency.SelectedItem).Clone();

            cboDVBSCollectionType.Text = currentSatelliteFrequency.CollectionType.ToString();

            cboLNBType.Enabled = currentSatelliteFrequency.LNBConversion;
            cboLNBType.Text = LNBType.Legacy;
        }

        private void btAddSatellite_Click(object sender, EventArgs e)
        {
            bool updated = updateSatelliteFrequency(currentSatelliteFrequency, true);
            if (!updated)
                return;

            bool advancedResult = getAdvancedParameters(currentSatelliteFrequency);
            if (!advancedResult)
                return;

            ListViewItem newItem = new ListViewItem(currentSatelliteFrequency.ToString());
            newItem.Tag = currentSatelliteFrequency;
            newItem.SubItems.Add(((Satellite)cboSatellite.SelectedItem).Name);
            newItem.SubItems.Add("Satellite");
            newItem.SubItems.Add(currentSatelliteFrequency.CollectionType.ToString());

            foreach (ListViewItem oldItem in lvSelectedFrequencies.Items)
            {
                int index = lvSelectedFrequencies.Items.IndexOf(oldItem);

                SatelliteFrequency oldFrequency = oldItem.Tag as SatelliteFrequency;
                if (oldFrequency != null && oldFrequency.EqualTo(currentSatelliteFrequency, EqualityLevel.Identity))
                {
                    DialogResult result = MessageBox.Show("The frequency has already been selected." + Environment.NewLine + Environment.NewLine +
                        "Do you want to overwrite it?", "EPG Centre", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                    switch (result)
                    {
                        case DialogResult.Cancel:
                            return;
                        case DialogResult.Yes:
                            lvSelectedFrequencies.Items.Remove(oldItem);
                            lbScanningFrequencies.Items.Remove(oldItem.Tag);
                            lvSelectedFrequencies.Items.Insert(index, newItem);
                            lbScanningFrequencies.Items.Insert(index, currentSatelliteFrequency);
                            return;
                        default:
                            lvSelectedFrequencies.Items.Add(newItem);
                            lbScanningFrequencies.Items.Add(currentSatelliteFrequency);
                            return;
                    }
                }
            }

            lvSelectedFrequencies.Items.Add(newItem);
            lbScanningFrequencies.Items.Add(currentSatelliteFrequency);

            cboFilterFrequency.Items.Add(currentSatelliteFrequency);
            if (cboFilterFrequency.Items.Count == 1)
                cboFilterFrequency.SelectedIndex = 0;
        }

        private bool updateSatelliteFrequency(SatelliteFrequency item, bool showError)
        {
            item.CollectionType = (CollectionType)cboDVBSCollectionType.SelectedItem;

            item.SatelliteDish = new SatelliteDish();

            try
            {
                item.SatelliteDish.LNBLowBandFrequency = Int32.Parse(txtLNBLow.Text.Trim());
                item.SatelliteDish.LNBHighBandFrequency = Int32.Parse(txtLNBHigh.Text.Trim());
                item.SatelliteDish.LNBSwitchFrequency = Int32.Parse(txtLNBSwitch.Text.Trim());
            }
            catch (FormatException)
            {
                if (showError)
                    MessageBox.Show("A dish parameter is incorrect.", "EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Error);
                else
                {
                    item.SatelliteDish.LNBLowBandFrequency = Int32.MinValue;
                    item.SatelliteDish.LNBHighBandFrequency = Int32.MinValue;
                    item.SatelliteDish.LNBSwitchFrequency = Int32.MinValue;
                }

                return (false);
            }
            catch (OverflowException)
            {
                if (showError)
                    MessageBox.Show("A dish parameter is incorrect.", "EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Error);
                else
                {
                    item.SatelliteDish.LNBLowBandFrequency = Int32.MinValue;
                    item.SatelliteDish.LNBHighBandFrequency = Int32.MinValue;
                    item.SatelliteDish.LNBSwitchFrequency = Int32.MinValue;
                }

                return (false);
            }

            item.SatelliteDish.LNBType = LNBType.GetInstance(cboLNBType.Text);

            if (udDvbsSatIpFrontend.SelectedIndex != 0)
                item.SatIpFrontend = udDvbsSatIpFrontend.SelectedIndex;
            else
                item.SatIpFrontend = -1;

            setTuner(item, clbSatelliteTuners);

            if (cboDiseqc.SelectedIndex != 0)
            {
                item.DiseqcRunParamters.DiseqcSwitch = cboDiseqc.Text;

                item.DiseqcRunParamters.Options.Clear();

                if (cbUseSafeDiseqc.Checked)
                    item.DiseqcRunParamters.Options.Add(new OptionEntry(OptionName.UseSafeDiseqc));
                if (cbSwitchAfterPlay.Checked)
                    item.DiseqcRunParamters.Options.Add(new OptionEntry(OptionName.SwitchAfterPlay));
                if (cbSwitchAfterTune.Checked)
                    item.DiseqcRunParamters.Options.Add(new OptionEntry(OptionName.SwitchAfterTune));
                if (cbRepeatDiseqc.Checked)
                    item.DiseqcRunParamters.Options.Add(new OptionEntry(OptionName.RepeatDiseqc));

                if (DiseqcHandlerBase.IsGeneric(cboDiseqcHandler.Text))
                {
                    if (cbDisableDriverDiseqc.Checked)
                        item.DiseqcRunParamters.Options.Add(new OptionEntry(OptionName.DisableDriverDiseqc));
                    if (cbUseDiseqcCommands.Checked)
                        item.DiseqcRunParamters.Options.Add(new OptionEntry(OptionName.UseDiseqcCommand));
                }

                item.DiseqcRunParamters.DiseqcHandler = cboDiseqcHandler.SelectedIndex != 0 ? cboDiseqcHandler.Text : null;
            }
            else
                item.DiseqcRunParamters = null;

            return (true);
        }

        private void cboCountry_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cboCountry.SelectedItem != null)
            {
                cboArea.Items.Clear();
                foreach (Area area in ((Country)cboCountry.SelectedItem).Areas)
                    cboArea.Items.Add(area);
                cboArea.SelectedIndex = 0;
            }
        }

        private void cboArea_SelectedIndexChanged(object sender, EventArgs e)
        {
            TerrestrialProvider provider = TerrestrialProvider.FindProvider(cboCountry.Text, cboArea.Text);
            if (provider == null)
                return;

            cboDVBTScanningFrequency.Items.Clear();
            foreach (TuningFrequency tuningFrequency in provider.Frequencies)
                cboDVBTScanningFrequency.Items.Add(tuningFrequency);
            cboDVBTScanningFrequency.SelectedIndex = 0;
        }

        private void cboDVBTScanningFrequency_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!currentFrequencyChanging)
                currentTerrestrialFrequency = (TerrestrialFrequency)((TerrestrialFrequency)cboDVBTScanningFrequency.SelectedItem).Clone();
            cboDVBTCollectionType.Text = currentTerrestrialFrequency.CollectionType.ToString();
        }

        private void btAddTerrestrial_Click(object sender, EventArgs e)
        {
            updateTerrestrialFrequency(currentTerrestrialFrequency);

            bool advancedResult = getAdvancedParameters(currentTerrestrialFrequency);
            if (!advancedResult)
                return;

            ListViewItem newItem = new ListViewItem(currentTerrestrialFrequency.ToString());
            newItem.Tag = currentTerrestrialFrequency;
            newItem.SubItems.Add(cboCountry.Text + " " + cboArea.Text);
            newItem.SubItems.Add("Terrestrial");
            newItem.SubItems.Add(currentTerrestrialFrequency.CollectionType.ToString());

            foreach (ListViewItem oldItem in lvSelectedFrequencies.Items)
            {
                int index = lvSelectedFrequencies.Items.IndexOf(oldItem);

                TerrestrialFrequency oldFrequency = oldItem.Tag as TerrestrialFrequency;
                if (oldFrequency != null && oldFrequency.EqualTo(currentTerrestrialFrequency, EqualityLevel.Identity))
                {
                    DialogResult result = MessageBox.Show("The frequency has already been selected." + Environment.NewLine + Environment.NewLine +
                        "Do you want to overwrite it?", "EPG Centre", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                    switch (result)
                    {
                        case DialogResult.Cancel:
                            return;
                        case DialogResult.Yes:
                            lvSelectedFrequencies.Items.Remove(oldItem);
                            lbScanningFrequencies.Items.Remove(oldItem.Tag);
                            lvSelectedFrequencies.Items.Insert(index, newItem);
                            lbScanningFrequencies.Items.Insert(index, currentTerrestrialFrequency);
                            return;
                        default:
                            lvSelectedFrequencies.Items.Add(newItem);
                            lbScanningFrequencies.Items.Add(currentTerrestrialFrequency);
                            return;
                    }
                }
            }

            lvSelectedFrequencies.Items.Add(newItem);
            lbScanningFrequencies.Items.Add(currentTerrestrialFrequency);

            cboFilterFrequency.Items.Add(currentTerrestrialFrequency);
            if (cboFilterFrequency.Items.Count == 1)
                cboFilterFrequency.SelectedIndex = 0;
        }

        private void updateTerrestrialFrequency(TerrestrialFrequency terrestrialFrequency)
        {
            terrestrialFrequency.CollectionType = (CollectionType)cboDVBTCollectionType.SelectedItem;

            if (udDvbtSatIpFrontend.SelectedIndex != 0)
                terrestrialFrequency.SatIpFrontend = udDvbtSatIpFrontend.SelectedIndex;
            else
                terrestrialFrequency.SatIpFrontend = -1;

            setTuner(terrestrialFrequency, clbTerrestrialTuners);
        }

        private void cboCable_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cboCable.SelectedItem != null)
            {
                cboCableScanningFrequency.Items.Clear();
                foreach (TuningFrequency tuningFrequency in ((CableProvider)cboCable.SelectedItem).Frequencies)
                    cboCableScanningFrequency.Items.Add(tuningFrequency);
                cboCableScanningFrequency.SelectedIndex = 0;
            }
        }

        private void cboCableScanningFrequency_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!currentFrequencyChanging)
                currentCableFrequency = (CableFrequency)((CableFrequency)cboCableScanningFrequency.SelectedItem).Clone();
            cboCableCollectionType.Text = currentCableFrequency.CollectionType.ToString();
        }

        private void btAddCable_Click(object sender, EventArgs e)
        {
            updateCableFrequency(currentCableFrequency);

            bool advancedResult = getAdvancedParameters(currentCableFrequency);
            if (!advancedResult)
                return;

            ListViewItem newItem = new ListViewItem(currentCableFrequency.ToString());
            newItem.Tag = currentCableFrequency;
            newItem.SubItems.Add(((CableProvider)cboCable.SelectedItem).Name);
            newItem.SubItems.Add("Cable");
            newItem.SubItems.Add(currentCableFrequency.CollectionType.ToString());

            foreach (ListViewItem oldItem in lvSelectedFrequencies.Items)
            {
                int index = lvSelectedFrequencies.Items.IndexOf(oldItem);

                CableFrequency oldFrequency = oldItem.Tag as CableFrequency;
                if (oldFrequency != null && oldFrequency.EqualTo(currentCableFrequency, EqualityLevel.Identity))
                {
                    DialogResult result = MessageBox.Show("The frequency has already been selected." + Environment.NewLine + Environment.NewLine +
                        "Do you want to overwrite it?", "EPG Centre", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                    switch (result)
                    {
                        case DialogResult.Cancel:
                            return;
                        case DialogResult.Yes:
                            lvSelectedFrequencies.Items.Remove(oldItem);
                            lbScanningFrequencies.Items.Remove(oldItem.Tag);
                            lvSelectedFrequencies.Items.Insert(index, newItem);
                            lbScanningFrequencies.Items.Insert(index, currentCableFrequency);
                            return;
                        default:
                            lvSelectedFrequencies.Items.Add(newItem);
                            lbScanningFrequencies.Items.Add(currentCableFrequency);
                            return;
                    }
                }
            }

            lvSelectedFrequencies.Items.Add(newItem);
            lbScanningFrequencies.Items.Add(currentCableFrequency);

            cboFilterFrequency.Items.Add(currentCableFrequency);
            if (cboFilterFrequency.Items.Count == 1)
                cboFilterFrequency.SelectedIndex = 0;
        }

        private void updateCableFrequency(CableFrequency cableFrequency)
        {
            cableFrequency.CollectionType = (CollectionType)cboCableCollectionType.SelectedItem;
            if (udDvbcSatIpFrontend.SelectedIndex != 0)
                cableFrequency.SatIpFrontend = udDvbcSatIpFrontend.SelectedIndex;
            else
                cableFrequency.SatIpFrontend = -1;

            setTuner(cableFrequency, clbCableTuners);
        }

        private void cboAtscProvider_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cboAtscProvider.SelectedItem != null)
            {
                cboAtscScanningFrequency.Items.Clear();
                foreach (TuningFrequency tuningFrequency in ((AtscProvider)cboAtscProvider.SelectedItem).Frequencies)
                    cboAtscScanningFrequency.Items.Add(tuningFrequency);
                cboAtscScanningFrequency.SelectedIndex = 0;
            }
        }

        private void cboAtscScanningFrequency_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!currentFrequencyChanging)
                currentAtscFrequency = (AtscFrequency)((AtscFrequency)cboAtscScanningFrequency.SelectedItem).Clone();
            cboAtscCollectionType.Text = currentAtscFrequency.CollectionType.ToString();
        }

        private void btAddAtsc_Click(object sender, EventArgs e)
        {
            updateAtscFrequency(currentAtscFrequency);

            bool advancedResult = getAdvancedParameters(currentAtscFrequency);
            if (!advancedResult)
                return;

            ListViewItem newItem = new ListViewItem(currentAtscFrequency.ToString());
            newItem.Tag = currentAtscFrequency;
            newItem.SubItems.Add(((AtscProvider)cboAtscProvider.SelectedItem).Name);
            newItem.SubItems.Add("ATSC");
            newItem.SubItems.Add(currentAtscFrequency.CollectionType.ToString());

            foreach (ListViewItem oldItem in lvSelectedFrequencies.Items)
            {
                int index = lvSelectedFrequencies.Items.IndexOf(oldItem);

                AtscFrequency oldFrequency = oldItem.Tag as AtscFrequency;
                if (oldFrequency != null && oldFrequency.EqualTo(currentAtscFrequency, EqualityLevel.Identity))
                {
                    DialogResult result = MessageBox.Show("The frequency has already been selected." + Environment.NewLine + Environment.NewLine +
                        "Do you want to overwrite it?", "EPG Centre", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                    switch (result)
                    {
                        case DialogResult.Cancel:
                            return;
                        case DialogResult.Yes:
                            lvSelectedFrequencies.Items.Remove(oldItem);
                            lbScanningFrequencies.Items.Remove(oldItem.Tag);
                            lvSelectedFrequencies.Items.Insert(index, newItem);
                            lbScanningFrequencies.Items.Insert(index, currentAtscFrequency);
                            return;
                        default:
                            lvSelectedFrequencies.Items.Add(newItem);
                            lbScanningFrequencies.Items.Add(currentAtscFrequency);
                            return;
                    }
                }
            }

            lvSelectedFrequencies.Items.Add(newItem);
            lbScanningFrequencies.Items.Add(currentAtscFrequency);

            cboFilterFrequency.Items.Add(currentAtscFrequency);
            if (cboFilterFrequency.Items.Count == 1)
                cboFilterFrequency.SelectedIndex = 0;
        }

        private void updateAtscFrequency(AtscFrequency atscFrequency)
        {
            atscFrequency.CollectionType = (CollectionType)cboAtscCollectionType.SelectedItem;
            setTuner(atscFrequency, clbAtscTuners);
        }

        private void cboClearQamProvider_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cboClearQamProvider.SelectedItem != null)
            {
                cboClearQamScanningFrequency.Items.Clear();
                foreach (TuningFrequency tuningFrequency in ((ClearQamProvider)cboClearQamProvider.SelectedItem).Frequencies)
                    cboClearQamScanningFrequency.Items.Add(tuningFrequency);
                cboClearQamScanningFrequency.SelectedIndex = 0;
            }
        }

        private void cboClearQamScanningFrequency_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!currentFrequencyChanging)
                currentClearQamFrequency = (ClearQamFrequency)((ClearQamFrequency)cboClearQamScanningFrequency.SelectedItem).Clone();
            cboClearQamCollectionType.Text = currentClearQamFrequency.CollectionType.ToString();
        }

        private void btAddClearQam_Click(object sender, EventArgs e)
        {
            updateClearQamFrequency(currentClearQamFrequency);

            bool advancedResult = getAdvancedParameters(currentClearQamFrequency);
            if (!advancedResult)
                return;

            ListViewItem newItem = new ListViewItem(currentClearQamFrequency.ToString());
            newItem.Tag = currentClearQamFrequency;
            newItem.SubItems.Add(((ClearQamProvider)cboClearQamProvider.SelectedItem).Name);
            newItem.SubItems.Add("Clear QAM");
            newItem.SubItems.Add(currentClearQamFrequency.CollectionType.ToString());

            foreach (ListViewItem oldItem in lvSelectedFrequencies.Items)
            {
                int index = lvSelectedFrequencies.Items.IndexOf(oldItem);

                ClearQamFrequency oldFrequency = oldItem.Tag as ClearQamFrequency;
                if (oldFrequency != null && oldFrequency.EqualTo(currentClearQamFrequency, EqualityLevel.Identity))
                {
                    DialogResult result = MessageBox.Show("The frequency has already been selected." + Environment.NewLine + Environment.NewLine +
                        "Do you want to overwrite it?", "EPG Centre", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                    switch (result)
                    {
                        case DialogResult.Cancel:
                            return;
                        case DialogResult.Yes:
                            lvSelectedFrequencies.Items.Remove(oldItem);
                            lbScanningFrequencies.Items.Remove(oldItem.Tag);
                            lvSelectedFrequencies.Items.Insert(index, newItem);
                            lbScanningFrequencies.Items.Insert(index, currentClearQamFrequency);
                            return;
                        default:
                            lvSelectedFrequencies.Items.Add(newItem);
                            lbScanningFrequencies.Items.Add(currentClearQamFrequency);
                            return;
                    }
                }
            }

            lvSelectedFrequencies.Items.Add(newItem);
            lbScanningFrequencies.Items.Add(currentClearQamFrequency);

            cboFilterFrequency.Items.Add(currentClearQamFrequency);
            if (cboFilterFrequency.Items.Count == 1)
                cboFilterFrequency.SelectedIndex = 0;
        }

        private void updateClearQamFrequency(ClearQamFrequency clearQamFrequency)
        {
            clearQamFrequency.CollectionType = (CollectionType)cboClearQamCollectionType.SelectedItem;
            setTuner(clearQamFrequency, clbClearQamTuners);
        }

        private void txtISDBLNBNumeric(object sender, KeyPressEventArgs e)
        {
            if ("0123456789\b".IndexOf(e.KeyChar) == -1)
                e.Handled = true;
        }

        private void txtISDBLNBLow_TextChanged(object sender, EventArgs e)
        {
            btAddSatellite.Enabled = txtLNBLow.Text.Length != 0 && txtLNBHigh.Text.Length != 0 && txtLNBSwitch.Text.Length != 0;
            txtLNBLow.BackColor = txtLNBLow.Text.Length != 0 ? Color.White : Color.Red;
        }

        private void txtISDBLNBHigh_TextChanged(object sender, EventArgs e)
        {
            btAddSatellite.Enabled = txtLNBLow.Text.Length != 0 && txtLNBHigh.Text.Length != 0 && txtLNBSwitch.Text.Length != 0;
            txtLNBHigh.BackColor = txtLNBHigh.Text.Length != 0 ? Color.White : Color.Red;
        }

        private void txtISDBLNBSwitch_TextChanged(object sender, EventArgs e)
        {
            btAddSatellite.Enabled = txtLNBLow.Text.Length != 0 && txtLNBHigh.Text.Length != 0 && txtLNBSwitch.Text.Length != 0;
            txtLNBSwitch.BackColor = txtLNBSwitch.Text.Length != 0 ? Color.White : Color.Red;
        }

        private void btISDBLNBDefaults_Click(object sender, EventArgs e)
        {
            SatelliteDish defaultSatellite = SatelliteDish.Default;

            txtISDBLNBLow.Text = defaultSatellite.LNBLowBandFrequency.ToString();
            txtISDBLNBHigh.Text = defaultSatellite.LNBHighBandFrequency.ToString();
            txtISDBLNBSwitch.Text = defaultSatellite.LNBSwitchFrequency.ToString();
        }

        private void cboISDBSatellite_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cboISDBSatellite.SelectedItem != null)
            {
                cboISDBSScanningFrequency.Items.Clear();
                foreach (TuningFrequency tuningFrequency in ((ISDBSatelliteProvider)cboISDBSatellite.SelectedItem).Frequencies)
                    cboISDBSScanningFrequency.Items.Add(tuningFrequency);
                cboISDBSScanningFrequency.SelectedIndex = 0;
            }
        }

        private void cboISDBSScanningFrequency_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!currentFrequencyChanging)
                currentISDBSatelliteFrequency = (ISDBSatelliteFrequency)((ISDBSatelliteFrequency)cboISDBSScanningFrequency.SelectedItem).Clone();
            cboISDBSCollectionType.Text = currentISDBSatelliteFrequency.CollectionType.ToString();
        }

        private void btAddISDBSatellite_Click(object sender, EventArgs e)
        {
            bool updated = updateISDBSatelliteFrequency(currentISDBSatelliteFrequency);

            bool advancedResult = getAdvancedParameters(currentISDBSatelliteFrequency);
            if (!advancedResult)
                return;

            ListViewItem newItem = new ListViewItem(currentISDBSatelliteFrequency.ToString());
            newItem.Tag = currentISDBSatelliteFrequency;
            newItem.SubItems.Add(((ISDBSatelliteProvider)cboISDBSatellite.SelectedItem).Name);
            newItem.SubItems.Add("ISDB Satellite");
            newItem.SubItems.Add(currentISDBSatelliteFrequency.CollectionType.ToString());
            newItem.SubItems.Add(currentISDBSatelliteFrequency.SatelliteDish.LNBLowBandFrequency.ToString());
            newItem.SubItems.Add(currentISDBSatelliteFrequency.SatelliteDish.LNBHighBandFrequency.ToString());
            newItem.SubItems.Add(currentISDBSatelliteFrequency.SatelliteDish.LNBSwitchFrequency.ToString());
            if (currentISDBSatelliteFrequency.DiseqcRunParamters.DiseqcSwitch != null)
                newItem.SubItems.Add(currentISDBSatelliteFrequency.DiseqcRunParamters.DiseqcSwitch);

            foreach (ListViewItem oldItem in lvSelectedFrequencies.Items)
            {
                int index = lvSelectedFrequencies.Items.IndexOf(oldItem);

                ISDBSatelliteFrequency oldFrequency = oldItem.Tag as ISDBSatelliteFrequency;
                if (oldFrequency != null && oldFrequency.EqualTo(currentISDBSatelliteFrequency, EqualityLevel.Identity))
                {
                    DialogResult result = MessageBox.Show("The frequency has already been selected." + Environment.NewLine + Environment.NewLine +
                        "Do you want to overwrite it?", "EPG Centre", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                    switch (result)
                    {
                        case DialogResult.Cancel:
                            return;
                        case DialogResult.Yes:
                            lvSelectedFrequencies.Items.Remove(oldItem);
                            lbScanningFrequencies.Items.Remove(oldItem.Tag);
                            lvSelectedFrequencies.Items.Insert(index, newItem);
                            lbScanningFrequencies.Items.Insert(index, currentISDBSatelliteFrequency);
                            return;
                        default:
                            lvSelectedFrequencies.Items.Add(newItem);
                            lbScanningFrequencies.Items.Add(currentISDBSatelliteFrequency);
                            return;
                    }
                }
            }

            lvSelectedFrequencies.Items.Add(newItem);
            lbScanningFrequencies.Items.Add(currentISDBSatelliteFrequency);

            cboFilterFrequency.Items.Add(currentISDBSatelliteFrequency);
            if (cboFilterFrequency.Items.Count == 1)
                cboFilterFrequency.SelectedIndex = 0;
        }

        private bool updateISDBSatelliteFrequency(ISDBSatelliteFrequency isdbSatelliteFrequency)
        {
            isdbSatelliteFrequency.CollectionType = (CollectionType)cboISDBSCollectionType.SelectedItem;

            isdbSatelliteFrequency.SatelliteDish = new SatelliteDish();

            try
            {
                isdbSatelliteFrequency.SatelliteDish.LNBLowBandFrequency = Int32.Parse(txtISDBLNBLow.Text.Trim());
                isdbSatelliteFrequency.SatelliteDish.LNBHighBandFrequency = Int32.Parse(txtISDBLNBHigh.Text.Trim());
                isdbSatelliteFrequency.SatelliteDish.LNBSwitchFrequency = Int32.Parse(txtISDBLNBSwitch.Text.Trim());
            }
            catch (FormatException)
            {
                MessageBox.Show("A dish parameter is incorrect.", "EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return (false);
            }
            catch (OverflowException)
            {
                MessageBox.Show("A dish parameter is incorrect.", "EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return (false);
            }

            setTuner(isdbSatelliteFrequency, clbISDBSatelliteTuners);

            return (true);
        }

        private void cboISDBTProvider_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cboISDBTProvider.SelectedItem != null)
            {
                cboISDBTScanningFrequency.Items.Clear();
                foreach (TuningFrequency tuningFrequency in ((ISDBTerrestrialProvider)cboISDBTProvider.SelectedItem).Channels)
                    cboISDBTScanningFrequency.Items.Add(tuningFrequency);
                cboISDBTScanningFrequency.SelectedIndex = 0;
            }
        }

        private void cboISDBTScanningFrequency_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!currentFrequencyChanging)
                currentISDBTerrestrialFrequency = (ISDBTerrestrialFrequency)((ISDBTerrestrialFrequency)cboISDBTScanningFrequency.SelectedItem).Clone();
            cboISDBTCollectionType.Text = currentISDBTerrestrialFrequency.CollectionType.ToString();
        }

        private void btAddISDBTerrestrial_Click(object sender, EventArgs e)
        {
            updateISDBTerrestrialFrequency(currentISDBTerrestrialFrequency);

            bool advancedResult = getAdvancedParameters(currentISDBTerrestrialFrequency);
            if (!advancedResult)
                return;

            ListViewItem newItem = new ListViewItem(currentISDBTerrestrialFrequency.ToString());
            newItem.Tag = currentISDBTerrestrialFrequency;
            newItem.SubItems.Add(((ISDBTerrestrialProvider)cboISDBTProvider.SelectedItem).Name);
            newItem.SubItems.Add("ISDB Terrestrial");
            newItem.SubItems.Add(currentISDBTerrestrialFrequency.CollectionType.ToString());

            foreach (ListViewItem oldItem in lvSelectedFrequencies.Items)
            {
                int index = lvSelectedFrequencies.Items.IndexOf(oldItem);

                ISDBTerrestrialFrequency oldFrequency = oldItem.Tag as ISDBTerrestrialFrequency;
                if (oldFrequency != null && oldFrequency.EqualTo(currentISDBTerrestrialFrequency, EqualityLevel.Identity))
                {
                    DialogResult result = MessageBox.Show("The frequency has already been selected." + Environment.NewLine + Environment.NewLine +
                        "Do you want to overwrite it?", "EPG Centre", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                    switch (result)
                    {
                        case DialogResult.Cancel:
                            return;
                        case DialogResult.Yes:
                            lvSelectedFrequencies.Items.Remove(oldItem);
                            lbScanningFrequencies.Items.Remove(oldItem.Tag);
                            lvSelectedFrequencies.Items.Insert(index, newItem);
                            lbScanningFrequencies.Items.Insert(index, currentISDBTerrestrialFrequency);
                            return;
                        default:
                            lvSelectedFrequencies.Items.Add(newItem);
                            lbScanningFrequencies.Items.Add(currentISDBTerrestrialFrequency);
                            return;
                    }
                }
            }

            lvSelectedFrequencies.Items.Add(newItem);
            lbScanningFrequencies.Items.Add(currentISDBTerrestrialFrequency);

            cboFilterFrequency.Items.Add(currentISDBTerrestrialFrequency);
            if (cboFilterFrequency.Items.Count == 1)
                cboFilterFrequency.SelectedIndex = 0;
        }

        private void updateISDBTerrestrialFrequency(ISDBTerrestrialFrequency isdbTerrestrialFrequency)
        {
            isdbTerrestrialFrequency.CollectionType = (CollectionType)cboISDBTCollectionType.SelectedItem;
            setTuner(isdbTerrestrialFrequency, clbISDBTerrestrialTuners);
        }

        private void tbDeliveryFilePath_TextChanged(object sender, EventArgs e)
        {
            btDeliveryFileAdd.Enabled = !string.IsNullOrWhiteSpace(tbDeliveryFilePath.Text);
        }

        private void tbDeliveryFileBrowse_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFile = new OpenFileDialog();
            openFile.Filter = "Transport Stream Files (*.ts)|*.ts";

            if (MainWindow.CurrentTSPath == null)
                openFile.InitialDirectory = RunParameters.DataDirectory;
            else
                openFile.InitialDirectory = MainWindow.CurrentTSPath;

            openFile.RestoreDirectory = true;
            openFile.Title = "Find Transport Stream Dump File";

            DialogResult result = openFile.ShowDialog();
            if (result == DialogResult.Cancel)
                return;

            tbDeliveryFilePath.Text = openFile.FileName;
        }

        private void btDeliveryFileAdd_Click(object sender, EventArgs e)
        {
            if (currentFileFrequency == null)
                currentFileFrequency = new FileFrequency();

            updateFileFrequency(currentFileFrequency);

            bool advancedResult = getAdvancedParameters(currentFileFrequency);
            if (!advancedResult)
                return;

            ListViewItem newItem = new ListViewItem(string.Empty);
            newItem.Tag = currentFileFrequency.Clone();
            newItem.SubItems.Add(currentFileFrequency.Path);
            newItem.SubItems.Add("File");
            newItem.SubItems.Add(currentFileFrequency.CollectionType.ToString());

            foreach (ListViewItem oldItem in lvSelectedFrequencies.Items)
            {
                int index = lvSelectedFrequencies.Items.IndexOf(oldItem);

                FileFrequency oldFrequency = oldItem.Tag as FileFrequency;
                if (oldFrequency != null && oldFrequency.EqualTo(currentFileFrequency, EqualityLevel.Identity))
                {
                    DialogResult result = MessageBox.Show("The file has already been selected." + Environment.NewLine + Environment.NewLine +
                        "Do you want to overwrite it?", "EPG Centre", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                    switch (result)
                    {
                        case DialogResult.Cancel:
                            return;
                        case DialogResult.Yes:
                            lvSelectedFrequencies.Items.Remove(oldItem);
                            lbScanningFrequencies.Items.Remove(oldItem.Tag);
                            lvSelectedFrequencies.Items.Insert(index, newItem);
                            lbScanningFrequencies.Items.Insert(index, currentFileFrequency);
                            return;
                        default:
                            lvSelectedFrequencies.Items.Add(newItem);
                            lbScanningFrequencies.Items.Add(currentFileFrequency);
                            return;
                    }
                }
            }

            lvSelectedFrequencies.Items.Add(newItem);
            lbScanningFrequencies.Items.Add(currentFileFrequency);
        }

        private void updateFileFrequency(FileFrequency fileFrequency)
        {
            fileFrequency.Path = tbDeliveryFilePath.Text.Trim();
            fileFrequency.CollectionType = (CollectionType)cboDeliveryFileCollectionType.SelectedItem;
        }

        private void btStreamAdd_Click(object sender, EventArgs e)
        {
            if (currentStreamFrequency == null)
                currentStreamFrequency = new StreamFrequency();

            bool updated = updateStreamFrequency(currentStreamFrequency, true);
            if (!updated)
                return;

            bool advancedResult = getAdvancedParameters(currentStreamFrequency);
            if (!advancedResult)
                return;

            ListViewItem newItem = new ListViewItem(string.Empty);
            newItem.Tag = currentStreamFrequency.Clone();
            newItem.SubItems.Add(currentStreamFrequency.ToString());
            newItem.SubItems.Add("Stream");
            newItem.SubItems.Add(currentStreamFrequency.CollectionType.ToString());

            foreach (ListViewItem oldItem in lvSelectedFrequencies.Items)
            {
                int index = lvSelectedFrequencies.Items.IndexOf(oldItem);

                StreamFrequency oldFrequency = oldItem.Tag as StreamFrequency;
                if (oldFrequency != null && oldFrequency.EqualTo(currentStreamFrequency, EqualityLevel.Identity))
                {
                    DialogResult result = MessageBox.Show("The stream has already been selected." + Environment.NewLine + Environment.NewLine +
                        "Do you want to overwrite it?", "EPG Centre", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                    switch (result)
                    {
                        case DialogResult.Cancel:
                            return;
                        case DialogResult.Yes:
                            lvSelectedFrequencies.Items.Remove(oldItem);
                            lbScanningFrequencies.Items.Remove(oldItem.Tag);
                            lvSelectedFrequencies.Items.Insert(index, newItem);
                            lbScanningFrequencies.Items.Insert(index, currentStreamFrequency);
                            return;
                        default:
                            lvSelectedFrequencies.Items.Add(newItem);
                            lbScanningFrequencies.Items.Add(currentStreamFrequency);
                            return;
                    }
                }
            }

            lvSelectedFrequencies.Items.Add(newItem);
            lbScanningFrequencies.Items.Add(currentStreamFrequency);
        }

        private bool updateStreamFrequency(StreamFrequency streamFrequency, bool showError)
        {
            string reply = StreamFrequency.ValidateIPAddress(tbStreamIpAddress.Text.Trim(), cboStreamProtocol.Text);
            if (reply != null)
            {
                if (showError)
                    MessageBox.Show(reply, "EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return (false);
            }

            if (!string.IsNullOrWhiteSpace(tbStreamMulticastSourceIP.Text))
            {
                reply = StreamFrequency.ValidateIPAddress(tbStreamMulticastSourceIP.Text.Trim(), null);
                if (reply != null)
                {
                    if (showError)
                        MessageBox.Show(reply, "EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return (false);
                }
            }

            if (!string.IsNullOrWhiteSpace(tbStreamIpAddress.Text))
                streamFrequency.IPAddress = tbStreamIpAddress.Text.Trim();
            else
                streamFrequency.IPAddress = "0.0.0.0";
            streamFrequency.PortNumber = (int)nudStreamPortNumber.Value;

            if (!string.IsNullOrWhiteSpace(tbStreamMulticastSourceIP.Text))
                streamFrequency.MulticastSource = tbStreamMulticastSourceIP.Text.Trim();
            streamFrequency.MulticastPort = (int)nudStreamMulticastSourcePort.Value;

            streamFrequency.Protocol = (StreamProtocol)Enum.Parse(typeof(StreamProtocol), cboStreamProtocol.Text);

            if (!string.IsNullOrWhiteSpace(tbStreamPath.Text))
                streamFrequency.Path = tbStreamPath.Text.Trim();

            streamFrequency.CollectionType = (CollectionType)cboStreamCollectionType.SelectedItem;

            return (true);
        }

        private void btFindIPAddress_Click(object sender, EventArgs e)
        {
            Cursor.Current = Cursors.WaitCursor;

            FindIPAddress findAddress = new FindIPAddress();
            DialogResult result = findAddress.ShowDialog();

            Cursor.Current = Cursors.Default;

            if (result == DialogResult.Cancel)
                return;

            tbStreamIpAddress.Text = findAddress.SelectedAddress.ToString();
        }

        private bool getAdvancedParameters(TuningFrequency tuningFrequency)
        {
            AdvancedParameters advancedParameters = new AdvancedParameters();
            advancedParameters.Initialize(tuningFrequency);
            DialogResult advancedResult = advancedParameters.ShowDialog();
            if (advancedResult == DialogResult.Cancel)
            {
                MessageBox.Show("The frequency has NOT been added to the selected list.", "EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return (false); ;
            }

            return (true);
        }

        private void lvSelectedFrequencies_DoubleClick(object sender, MouseEventArgs e)
        {
            ListViewHitTestInfo hitTestInfo = lvSelectedFrequencies.HitTest(e.X, e.Y);
            ListViewItem item = hitTestInfo.Item;
            if (item == null)
                return;

            TuningFrequency tuningFrequency = item.Tag as TuningFrequency;
            if (tuningFrequency == null)
                return;

            processFrequencyChange(tuningFrequency);
        }

        private void processFrequencyChange(TuningFrequency tuningFrequency)
        {
            switch (tuningFrequency.TunerType)
            {
                case TunerType.Satellite:
                    FillSatelliteDetails((SatelliteFrequency)tuningFrequency);
                    tbcDeliverySystem.SelectedTab = tbcDeliverySystem.TabPages["tbpSatellite"];
                    break;
                case TunerType.Terrestrial:
                    FillTerrestrialDetails((TerrestrialFrequency)tuningFrequency);
                    tbcDeliverySystem.SelectedTab = tbcDeliverySystem.TabPages["tbpTerrestrial"];
                    break;
                case TunerType.Cable:
                    FillCableDetails((CableFrequency)tuningFrequency);
                    tbcDeliverySystem.SelectedTab = tbcDeliverySystem.TabPages["tbpCable"];
                    break;
                case TunerType.ATSC:
                case TunerType.ATSCCable:
                    FillAtscDetails((AtscFrequency)tuningFrequency);
                    tbcDeliverySystem.SelectedTab = tbcDeliverySystem.TabPages["tbpAtsc"];
                    break;
                case TunerType.ClearQAM:
                    FillClearQamDetails((ClearQamFrequency)tuningFrequency);
                    tbcDeliverySystem.SelectedTab = tbcDeliverySystem.TabPages["tbpClearQam"];
                    break;
                case TunerType.ISDBS:
                    FillISDBSatelliteDetails((ISDBSatelliteFrequency)tuningFrequency);
                    tbcDeliverySystem.SelectedTab = tbcDeliverySystem.TabPages["tbpISDBSatellite"];
                    break;
                case TunerType.ISDBT:
                    FillISDBTerrestrialDetails((ISDBTerrestrialFrequency)tuningFrequency);
                    tbcDeliverySystem.SelectedTab = tbcDeliverySystem.TabPages["tbpISDBTerrestrial"];
                    break;
                case TunerType.File:
                    FillFileDetails((FileFrequency)tuningFrequency);
                    tbcDeliverySystem.SelectedTab = tbcDeliverySystem.TabPages["tbpFile"];
                    break;
                case TunerType.Stream:
                    FillStreamDetails((StreamFrequency)tuningFrequency);
                    tbcDeliverySystem.SelectedTab = tbcDeliverySystem.TabPages["tbpStream"];
                    break;
                default:
                    break;
            }
        }

        private void lvSelectedFrequencies_SelectedIndexChanged(object sender, EventArgs e)
        {
            btDelete.Enabled = (lvSelectedFrequencies.SelectedItems.Count != 0);
            btChange.Enabled = (lvSelectedFrequencies.SelectedItems.Count != 0);
            btTuningParameters.Enabled = (lvSelectedFrequencies.SelectedItems.Count != 0);

            if (lvSelectedFrequencies.SelectedItems.Count == 0)
                btSelectedFrequencyDetails.Enabled = false;
            else
            {
                FileFrequency fileFrequency = lvSelectedFrequencies.SelectedItems[0].Tag as FileFrequency;
                StreamFrequency streamFrequency = lvSelectedFrequencies.SelectedItems[0].Tag as StreamFrequency;

                btSelectedFrequencyDetails.Enabled = (fileFrequency == null && streamFrequency == null);
            }
        }

        private void btDelete_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem item in lvSelectedFrequencies.SelectedItems)
            {
                lvSelectedFrequencies.Items.Remove(item);
                lbScanningFrequencies.Items.Remove(item.Tag);
                cboFilterFrequency.Items.Remove(item.Tag);
            }

            btDelete.Enabled = (lvSelectedFrequencies.SelectedItems.Count != 0);
            btChange.Enabled = (lvSelectedFrequencies.SelectedItems.Count != 0);
        }

        private void btChange_Click(object sender, EventArgs e)
        {
            if (lvSelectedFrequencies.SelectedItems.Count != 1)
            {
                MessageBox.Show("Please select a single frequency", " EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            processFrequencyChange(lvSelectedFrequencies.SelectedItems[0].Tag as TuningFrequency);
        }

        private void btSelectedFrequencyDetails_Click(object sender, EventArgs e)
        {
            if (lvSelectedFrequencies.SelectedItems.Count != 1)
            {
                MessageBox.Show("Please select a single frequency", " EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            SatelliteFrequency satelliteFrequency = lvSelectedFrequencies.SelectedItems[0].Tag as SatelliteFrequency;
            if (satelliteFrequency != null)
            {
                Satellite satellite = Satellite.FindProvider(satelliteFrequency.Provider.Name);
                SatelliteFrequency frequency = satellite.FindFrequency(satelliteFrequency.Frequency, satelliteFrequency.Polarization) as SatelliteFrequency;

                ChangeDvbsDetails changeDvbs = new ChangeDvbsDetails();
                changeDvbs.Initialize(satellite, frequency, false);
                DialogResult changeResult = changeDvbs.ShowDialog();
                if (changeResult == DialogResult.Cancel)
                    return;

                frequency = satellite.FindFrequency(satelliteFrequency.Frequency, satelliteFrequency.Polarization) as SatelliteFrequency;
                UpdateSelectedFrequency(frequency);

                return;
            }

            TerrestrialFrequency terrestrialFrequency = lvSelectedFrequencies.SelectedItems[0].Tag as TerrestrialFrequency;
            if (terrestrialFrequency != null)
            {
                TerrestrialProvider provider = TerrestrialProvider.FindProvider(terrestrialFrequency.Provider.Name);
                TerrestrialFrequency frequency = provider.FindFrequency(terrestrialFrequency.Frequency) as TerrestrialFrequency;

                ChangeDvbtDetails changeDvbt = new ChangeDvbtDetails();
                changeDvbt.Initialize(provider.Country.Name, provider.Area.Name, frequency, false);
                DialogResult changeResult = changeDvbt.ShowDialog();
                if (changeResult == DialogResult.Cancel)
                    return;

                frequency = provider.FindFrequency(terrestrialFrequency.Frequency) as TerrestrialFrequency;
                UpdateSelectedFrequency(frequency);

                return;
            }

            CableFrequency cableFrequency = lvSelectedFrequencies.SelectedItems[0].Tag as CableFrequency;
            if (cableFrequency != null)
            {
                CableProvider provider = CableProvider.FindProvider(cableFrequency.Provider.Name);
                CableFrequency frequency = provider.FindFrequency(cableFrequency.Frequency) as CableFrequency;

                ChangeDvbcDetails changeDvbc = new ChangeDvbcDetails();
                changeDvbc.Initialize(provider, frequency, false);
                DialogResult changeResult = changeDvbc.ShowDialog();
                if (changeResult == DialogResult.Cancel)
                    return;

                frequency = provider.FindFrequency(cableFrequency.Frequency) as CableFrequency;
                UpdateSelectedFrequency(frequency);

                return;
            }

            AtscFrequency atscFrequency = lvSelectedFrequencies.SelectedItems[0].Tag as AtscFrequency;
            if (atscFrequency != null)
            {
                AtscProvider provider = AtscProvider.FindProvider(atscFrequency.Provider.Name);
                AtscFrequency frequency = provider.FindFrequency(atscFrequency.Frequency) as AtscFrequency;

                ChangeAtscDetails changeAtsc = new ChangeAtscDetails();
                changeAtsc.Initialize(provider, frequency, false);
                DialogResult changeResult = changeAtsc.ShowDialog();
                if (changeResult == DialogResult.Cancel)
                    return;

                frequency = provider.FindFrequency(atscFrequency.Frequency) as AtscFrequency;
                UpdateSelectedFrequency(frequency);

                return;
            }

            ClearQamFrequency clearQamFrequency = lvSelectedFrequencies.SelectedItems[0].Tag as ClearQamFrequency;
            if (clearQamFrequency != null)
            {
                ClearQamProvider provider = ClearQamProvider.FindProvider(clearQamFrequency.Provider.Name);
                ClearQamFrequency frequency = provider.FindFrequency(clearQamFrequency.Frequency) as ClearQamFrequency;

                ChangeClearQamDetails changeClearQam = new ChangeClearQamDetails();
                changeClearQam.Initialize(provider, frequency, false);
                DialogResult changeResult = changeClearQam.ShowDialog();
                if (changeResult == DialogResult.Cancel)
                    return;

                frequency = provider.FindFrequency(clearQamFrequency.Frequency) as ClearQamFrequency;
                UpdateSelectedFrequency(frequency);

                return;
            }

            ISDBSatelliteFrequency isdbsFrequency = lvSelectedFrequencies.SelectedItems[0].Tag as ISDBSatelliteFrequency;
            if (isdbsFrequency != null)
            {
                ISDBSatelliteProvider provider = ISDBSatelliteProvider.FindProvider(isdbsFrequency.Provider.Name);
                ISDBSatelliteFrequency frequency = provider.FindFrequency(isdbsFrequency.Frequency, isdbsFrequency.Polarization) as ISDBSatelliteFrequency;

                ChangeIsdbsDetails changeIsdbs = new ChangeIsdbsDetails();
                changeIsdbs.Initialize(provider, frequency, false);
                DialogResult changeResult = changeIsdbs.ShowDialog();
                if (changeResult == DialogResult.Cancel)
                    return;

                frequency = provider.FindFrequency(isdbsFrequency.Frequency, isdbsFrequency.Polarization) as ISDBSatelliteFrequency;
                UpdateSelectedFrequency(frequency);

                return;
            }

            ISDBTerrestrialFrequency isdbtFrequency = lvSelectedFrequencies.SelectedItems[0].Tag as ISDBTerrestrialFrequency;
            if (isdbtFrequency != null)
            {
                ISDBTerrestrialProvider provider = ISDBTerrestrialProvider.FindProvider(isdbtFrequency.Provider.Name);
                ISDBTerrestrialFrequency frequency = provider.FindFrequency(isdbtFrequency.Frequency) as ISDBTerrestrialFrequency;

                ChangeIsdbtDetails changeIsdbt = new ChangeIsdbtDetails();
                changeIsdbt.Initialize(provider, frequency, false);
                DialogResult changeResult = changeIsdbt.ShowDialog();
                if (changeResult == DialogResult.Cancel)
                    return;

                frequency = provider.FindFrequency(isdbtFrequency.Frequency) as ISDBTerrestrialFrequency;
                UpdateSelectedFrequency(frequency);

                return;
            }

            btDelete.Enabled = false;
            btChange.Enabled = false;
            btSelectedFrequencyDetails.Enabled = false;
            btTuningParameters.Enabled = false;

            MessageBox.Show("No details available for the delivery system.", " EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        internal void UpdateSelectedFrequency(SatelliteFrequency updatedFrequency)
        {
            foreach (ListViewItem item in lvSelectedFrequencies.Items)
            {
                SatelliteFrequency oldFrequency = item.Tag as SatelliteFrequency;
                if (oldFrequency != null)
                {
                    if (oldFrequency.EqualTo(updatedFrequency, EqualityLevel.Identity))
                    {
                        oldFrequency.FEC = updatedFrequency.FEC;
                        oldFrequency.Modulation = updatedFrequency.Modulation;
                        oldFrequency.Pilot = updatedFrequency.Pilot;
                        oldFrequency.Polarization = updatedFrequency.Polarization;
                        oldFrequency.RollOff = updatedFrequency.RollOff;
                        oldFrequency.SymbolRate = updatedFrequency.SymbolRate;
                    }
                }
            }
        }

        internal void UpdateSelectedFrequency(TerrestrialFrequency updatedFrequency)
        {
            foreach (ListViewItem item in lvSelectedFrequencies.Items)
            {
                TerrestrialFrequency oldFrequency = item.Tag as TerrestrialFrequency;
                if (oldFrequency != null)
                {
                    if (oldFrequency.EqualTo(updatedFrequency, EqualityLevel.Identity))
                    {
                        oldFrequency.Bandwidth = updatedFrequency.Bandwidth;
                        oldFrequency.PlpNumber = updatedFrequency.PlpNumber;
                        oldFrequency.ChannelNumber = updatedFrequency.ChannelNumber;
                    }
                }
            }
        }

        internal void UpdateSelectedFrequency(CableFrequency updatedFrequency)
        {
            foreach (ListViewItem item in lvSelectedFrequencies.Items)
            {
                CableFrequency oldFrequency = item.Tag as CableFrequency;
                if (oldFrequency != null)
                {
                    if (oldFrequency.EqualTo(updatedFrequency, EqualityLevel.Identity))
                    {
                        oldFrequency.Modulation = updatedFrequency.Modulation;
                        oldFrequency.SymbolRate = updatedFrequency.SymbolRate;
                    }
                }
            }
        }

        internal void UpdateSelectedFrequency(AtscFrequency updatedFrequency)
        {
            foreach (ListViewItem item in lvSelectedFrequencies.Items)
            {
                AtscFrequency oldFrequency = item.Tag as AtscFrequency;
                if (oldFrequency != null)
                {
                    if (oldFrequency.EqualTo(updatedFrequency, EqualityLevel.Identity))
                    {
                        oldFrequency.ChannelNumber = updatedFrequency.ChannelNumber;
                        oldFrequency.FEC = updatedFrequency.FEC;
                        oldFrequency.Modulation = updatedFrequency.Modulation;
                        oldFrequency.SymbolRate = updatedFrequency.SymbolRate;
                    }
                }
            }
        }

        internal void UpdateSelectedFrequency(ClearQamFrequency updatedFrequency)
        {
            foreach (ListViewItem item in lvSelectedFrequencies.Items)
            {
                ClearQamFrequency oldFrequency = item.Tag as ClearQamFrequency;
                if (oldFrequency != null)
                {
                    if (oldFrequency.EqualTo(updatedFrequency, EqualityLevel.Identity))
                    {
                        oldFrequency.ChannelNumber = updatedFrequency.ChannelNumber;
                        oldFrequency.FEC = updatedFrequency.FEC;
                        oldFrequency.Modulation = updatedFrequency.Modulation;
                        oldFrequency.SymbolRate = updatedFrequency.SymbolRate;
                    }
                }
            }
        }

        internal void UpdateSelectedFrequency(ISDBSatelliteFrequency updatedFrequency)
        {
            foreach (ListViewItem item in lvSelectedFrequencies.Items)
            {
                ISDBSatelliteFrequency oldFrequency = item.Tag as ISDBSatelliteFrequency;
                if (oldFrequency != null)
                {
                    if (oldFrequency.EqualTo(updatedFrequency, EqualityLevel.Identity))
                    {
                        oldFrequency.FEC = updatedFrequency.FEC;
                        oldFrequency.Polarization = updatedFrequency.Polarization;
                        oldFrequency.SymbolRate = updatedFrequency.SymbolRate;
                    }
                }
            }
        }

        internal void UpdateSelectedFrequency(ISDBTerrestrialFrequency updatedFrequency)
        {
            foreach (ListViewItem item in lvSelectedFrequencies.Items)
            {
                ISDBTerrestrialFrequency oldFrequency = item.Tag as ISDBTerrestrialFrequency;
                if (oldFrequency != null)
                {
                    if (oldFrequency.EqualTo(updatedFrequency, EqualityLevel.Identity))
                    {
                        oldFrequency.Bandwidth = updatedFrequency.Bandwidth;
                        oldFrequency.ChannelNumber = updatedFrequency.ChannelNumber;
                    }
                }
            }
        }

        private void btTuningParameters_Click(object sender, EventArgs e)
        {
            TuningFrequency tuningFrequency = lvSelectedFrequencies.SelectedItems[0].Tag as TuningFrequency;

            AdvancedParameters advancedParameters = new AdvancedParameters();
            advancedParameters.Initialize(tuningFrequency);
            advancedParameters.ShowDialog();

            btDelete.Enabled = false;
            btChange.Enabled = false;
            btSelectedFrequencyDetails.Enabled = false;
            btTuningParameters.Enabled = false;
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog browseFile = new FolderBrowserDialog();
            browseFile.Description = "EPG Centre - Find Output File Directory";
            if (currentXmltvOutputPath == null)
                browseFile.SelectedPath = RunParameters.DataDirectory;
            else
                browseFile.SelectedPath = currentXmltvOutputPath;
            DialogResult result = browseFile.ShowDialog();
            if (result == DialogResult.Cancel)
                return;

            currentXmltvOutputPath = browseFile.SelectedPath;

            if (!browseFile.SelectedPath.EndsWith(Path.DirectorySeparatorChar.ToString()))
                txtOutputFile.Text = Path.Combine(browseFile.SelectedPath, "TVGuide.xml");
            else
                txtOutputFile.Text = browseFile.SelectedPath + "TVGuide.xml";
        }

        private void txtImportName_KeyPressAlphaNumeric(object sender, KeyPressEventArgs e)
        {
            Regex alphaNumericPattern = new Regex(@"[a-zA-Z0-9\s\b]");
            e.Handled = !alphaNumericPattern.IsMatch(e.KeyChar.ToString());
        }

        private void cbUseDVBViewer_CheckedChanged(object sender, EventArgs e)
        {
            if (cbUseDVBViewer.Checked)
            {
                cbDVBViewerImport.Checked = false;
                cbRecordingServiceImport.Checked = false;

                cbDVBViewerSubtitleVisible.Checked = false;
                cbDVBViewerSubtitleVisible.Enabled = false;
                cbDVBViewerClear.Checked = false;
                cbDVBViewerClear.Enabled = false;

                nudPort.Enabled = false;
            }
        }

        private void cbDVBViewerImport_CheckedChanged(object sender, EventArgs e)
        {
            if (cbDVBViewerImport.Checked)
            {
                cbUseDVBViewer.Checked = false;
                cbRecordingServiceImport.Checked = false;

                cbDVBViewerSubtitleVisible.Checked = false;
                cbDVBViewerSubtitleVisible.Enabled = false;
                cbDVBViewerClear.Checked = false;
                cbDVBViewerClear.Enabled = false;

                nudPort.Enabled = false;
            }
        }

        private void cbRecordingServiceImport_CheckedChanged(object sender, EventArgs e)
        {
            if (cbRecordingServiceImport.Checked)
            {
                cbUseDVBViewer.Checked = false;
                cbDVBViewerImport.Checked = false;

                cbDVBViewerSubtitleVisible.Enabled = true;
                cbDVBViewerClear.Enabled = true;
                nudPort.Enabled = true;
            }
            else
            {
                cbDVBViewerSubtitleVisible.Checked = false;
                cbDVBViewerSubtitleVisible.Enabled = false;
                cbDVBViewerClear.Checked = false;
                cbDVBViewerClear.Enabled = false;

                nudPort.Enabled = false;
            }
        }

        private void cbStoreStationInfo_CheckedChanged(object sender, EventArgs e)
        {
            if (cbStoreStationInfo.Checked)
                cbUseStoredStationInfo.Checked = false;

        }

        private void cbUseStoredStationInfo_CheckedChanged(object sender, EventArgs e)
        {
            if (cbUseStoredStationInfo.Checked)
                cbStoreStationInfo.Checked = false;
        }

        private void onCellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (bindingList[e.RowIndex].ExcludedByUser)
                e.CellStyle.ForeColor = Color.Red;
        }

        private void dgServices_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (dgServices.CurrentCell.ColumnIndex == dgServices.Columns["newNameColumn"].Index)
            {
                TextBox textEdit = e.Control as TextBox;
                textEdit.KeyPress -= new KeyPressEventHandler(textEdit_KeyPressAlphaNumeric);
                textEdit.KeyPress -= new KeyPressEventHandler(textEdit_KeyPressNumeric);
                textEdit.KeyPress += new KeyPressEventHandler(textEdit_KeyPressAlphaNumeric);
            }
            else
            {
                if (dgServices.CurrentCell.ColumnIndex == dgServices.Columns["logicalChannelNumberColumn"].Index)
                {
                    TextBox textEdit = e.Control as TextBox;
                    textEdit.KeyPress -= new KeyPressEventHandler(textEdit_KeyPressAlphaNumeric);
                    textEdit.KeyPress -= new KeyPressEventHandler(textEdit_KeyPressNumeric);
                    textEdit.KeyPress += new KeyPressEventHandler(textEdit_KeyPressNumeric);
                }
            }
        }

        private void textEdit_KeyPressAlphaNumeric(object sender, KeyPressEventArgs e)
        {
            Regex alphaNumericPattern = new Regex(@"[a-zA-Z0-9!&*()-+?\s\b]");
            e.Handled = !alphaNumericPattern.IsMatch(e.KeyChar.ToString());
        }

        private void textEdit_KeyPressNumeric(object sender, KeyPressEventArgs e)
        {
            if ("0123456789\b".IndexOf(e.KeyChar) == -1)
                e.Handled = true;
        }

        private void dgServices_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (dgServices.IsCurrentCellDirty && dgServices.Columns[dgServices.CurrentCell.ColumnIndex].Name == "excludedByUserColumn")
                dgServices.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }

        private void dgServices_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (dgServices.Columns[e.ColumnIndex].Name != "excludedByUserColumn")
                return;

            if (bindingList[e.RowIndex].ExcludedByUser)
            {
                foreach (DataGridViewCell cell in dgServices.Rows[e.RowIndex].Cells)
                {
                    cell.Style.ForeColor = Color.Red;
                    cell.Style.SelectionForeColor = Color.Red;
                }
            }
            else
            {
                foreach (DataGridViewCell cell in dgServices.Rows[e.RowIndex].Cells)
                {
                    cell.Style.ForeColor = Color.Black;
                    cell.Style.SelectionForeColor = Color.White;
                }
            }

            dgServices.Invalidate();
        }

        private void dgServices_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            dgServices.EndEdit();

            if (sortedColumnName == null)
            {
                sortedAscending = true;
                sortedColumnName = dgServices.Columns[e.ColumnIndex].Name;
            }
            else
            {
                if (sortedColumnName == dgServices.Columns[e.ColumnIndex].Name)
                    sortedAscending = !sortedAscending;
                else
                    sortedColumnName = dgServices.Columns[e.ColumnIndex].Name;
            }

            Collection<TVStation> sortedStations = new Collection<TVStation>();

            foreach (TVStation station in bindingList)
            {
                switch (dgServices.Columns[e.ColumnIndex].Name)
                {
                    case "nameColumn":
                        addInOrder(sortedStations, station, sortedAscending, "Name");
                        break;
                    case "originalNetworkIDColumn":
                        addInOrder(sortedStations, station, sortedAscending, "ONID");
                        break;
                    case "transportStreamIDColumn":
                        addInOrder(sortedStations, station, sortedAscending, "TSID");
                        break;
                    case "serviceIDColumn":
                        addInOrder(sortedStations, station, sortedAscending, "SID");
                        break;
                    case "excludedByUserColumn":
                        addInOrder(sortedStations, station, sortedAscending, "ExcludedByUser");
                        break;
                    case "newNameColumn":
                        addInOrder(sortedStations, station, sortedAscending, "NewName");
                        break;
                    case "logicalChannelNumberColumn":
                        addInOrder(sortedStations, station, sortedAscending, "ChannelNumber");
                        break;
                    default:
                        return;
                }
            }

            bindingList = new BindingList<TVStation>();
            foreach (TVStation station in sortedStations)
                bindingList.Add(station);

            tvStationBindingSource.DataSource = bindingList;
        }

        private void addInOrder(Collection<TVStation> stations, TVStation newStation, bool sortedAscending, string keyName)
        {
            sortedKeyName = keyName;

            foreach (TVStation oldStation in stations)
            {
                if (sortedAscending)
                {
                    if (oldStation.CompareForSorting(newStation, keyName) > 0)
                    {
                        stations.Insert(stations.IndexOf(oldStation), newStation);
                        return;
                    }
                }
                else
                {
                    if (oldStation.CompareForSorting(newStation, keyName) < 0)
                    {
                        stations.Insert(stations.IndexOf(oldStation), newStation);
                        return;
                    }
                }
            }

            stations.Add(newStation);
        }

        private void cmdClearScan_Click(object sender, EventArgs e)
        {
            bindingList.Clear();
            runParameters.StationCollection.Clear();
        }

        private void tbcParametersDeselecting(object sender, TabControlCancelEventArgs e)
        {
            if (e.TabPage == tabServices && pbarChannels.Enabled)
                e.Cancel = true;
        }

        private void cmdScan_Click(object sender, EventArgs e)
        {
            if (cmdScan.Text == "Stop Scan")
            {
                Logger.Instance.Write("Stop scan requested");
                workerScanStations.CancelAsync();
                bool reply = resetEvent.WaitOne(new TimeSpan(0, 0, 45));
                cmdClearScan.Enabled = (bindingList != null && bindingList.Count == 0);
                cmdScan.Text = "Start Scan";
                cmdSelectAll.Enabled = true;
                cmdSelectNone.Enabled = true;
                lblScanning.Visible = false;
                pbarChannels.Enabled = false;
                pbarChannels.Visible = false;

                btPlusScan.Text = "Start Scan";
                lblPlusScanning.Visible = false;
                pbarPlusScan.Enabled = false;
                pbarPlusScan.Visible = false;

                MainWindow.ChangeMenuItemAvailability(true);

                return;
            }

            Logger.Instance.Write("Scan started");

            scanningFrequencies = new Collection<TuningFrequency>();
            foreach (ListViewItem item in lvSelectedFrequencies.Items)
            {
                TuningFrequency tuningFrequency = item.Tag as TuningFrequency;
                scanningFrequencies.Add(tuningFrequency);
            }

            if (scanningFrequencies.Count == 0)
            {
                MessageBox.Show("No frequencies available to scan.", "EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!validateData())
                return;

            cmdClearScan.Enabled = false;
            cmdScan.Text = "Stop Scan";
            cmdSelectAll.Enabled = false;
            cmdSelectNone.Enabled = false;
            lblScanning.Visible = true;
            pbarChannels.Visible = true;
            pbarChannels.Enabled = true;

            btPlusScan.Text = "Stop Scan";
            lblPlusScanning.Visible = true;
            pbarPlusScan.Enabled = true;
            pbarPlusScan.Visible = true;

            MainWindow.ChangeMenuItemAvailability(false);

            setRunParameters();
            RunParameters.Instance = runParameters;

            if (scanningFrequencies[0].CollectionType != CollectionType.PSIP)
            {
                dgServices.Columns["originalNetworkIDColumn"].HeaderText = "ONID";
                dgServices.Columns["transportStreamIDColumn"].HeaderText = "TSID";
                dgServices.Columns["serviceIDColumn"].HeaderText = "SID";
                dgServices.Columns["logicalChannelNumberColumn"].Visible = true;
            }
            else
            {
                dgServices.Columns["originalNetworkIDColumn"].HeaderText = "Frequency";
                dgServices.Columns["transportStreamIDColumn"].HeaderText = "Channel";
                dgServices.Columns["serviceIDColumn"].HeaderText = "Sub-Channel";
                dgServices.Columns["logicalChannelNumberColumn"].Visible = false;
            }

            if (tbcParameters.SelectedTab.Name == "tabServices")
                ignoreTuningErrors = cbChannelTuningErrors.Checked;
            else
                ignoreTuningErrors = cbTimeshiftTuningErrors.Checked;

            workerScanStations = new BackgroundWorker();
            workerScanStations.WorkerReportsProgress = true;
            workerScanStations.WorkerSupportsCancellation = true;
            workerScanStations.DoWork += new DoWorkEventHandler(scanStationsDoScan);
            workerScanStations.RunWorkerCompleted += new RunWorkerCompletedEventHandler(scanStationsRunWorkerCompleted);
            workerScanStations.ProgressChanged += new ProgressChangedEventHandler(scanStationsProgressChanged);
            workerScanStations.RunWorkerAsync(scanningFrequencies);
        }

        private void scanStationsProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            lblScanning.Text = "Scanning " + scanningFrequencies[e.ProgressPercentage];
            lblPlusScanning.Text = "Scanning " + scanningFrequencies[e.ProgressPercentage];
        }

        private void scanStationsDoScan(object sender, DoWorkEventArgs e)
        {
            Collection<TuningFrequency> frequencies = e.Argument as Collection<TuningFrequency>;

            Logger.Instance.Write("Scanning " + frequencies.Count + " frequencies");

            foreach (TuningFrequency frequency in frequencies)
            {
                if ((sender as BackgroundWorker).CancellationPending)
                {
                    Logger.Instance.Write("Scan abandoned by user");
                    e.Cancel = true;
                    resetEvent.Set();
                    return;
                }

                Logger.Instance.Write("Scanning frequency " + frequency.ToString() + " on " + frequency.TunerType);
                (sender as BackgroundWorker).ReportProgress(frequencies.IndexOf(frequency));

                RunParameters.Instance.CurrentFrequency = frequency;

                TunerNodeType tunerNodeType;
                TuningSpec tuningSpec;

                SatelliteFrequency satelliteFrequency = frequency as SatelliteFrequency;
                if (satelliteFrequency != null)
                {
                    tunerNodeType = TunerNodeType.Satellite;
                    tuningSpec = new TuningSpec((Satellite)satelliteFrequency.Provider, satelliteFrequency);
                }
                else
                {
                    TerrestrialFrequency terrestrialFrequency = frequency as TerrestrialFrequency;
                    if (terrestrialFrequency != null)
                    {
                        tunerNodeType = TunerNodeType.Terrestrial;
                        tuningSpec = new TuningSpec(terrestrialFrequency);
                    }
                    else
                    {
                        CableFrequency cableFrequency = frequency as CableFrequency;
                        if (cableFrequency != null)
                        {
                            tunerNodeType = TunerNodeType.Cable;
                            tuningSpec = new TuningSpec(cableFrequency);
                        }
                        else
                        {
                            AtscFrequency atscFrequency = frequency as AtscFrequency;
                            if (atscFrequency != null)
                            {
                                if (atscFrequency.TunerType == TunerType.ATSC)
                                    tunerNodeType = TunerNodeType.ATSC;
                                else
                                    tunerNodeType = TunerNodeType.Cable;
                                tuningSpec = new TuningSpec(atscFrequency);
                            }
                            else
                            {
                                ClearQamFrequency clearQamFrequency = frequency as ClearQamFrequency;
                                if (clearQamFrequency != null)
                                {
                                    tunerNodeType = TunerNodeType.Cable;
                                    tuningSpec = new TuningSpec(clearQamFrequency);
                                }
                                else
                                {
                                    ISDBSatelliteFrequency isdbSatelliteFrequency = frequency as ISDBSatelliteFrequency;
                                    if (isdbSatelliteFrequency != null)
                                    {
                                        tunerNodeType = TunerNodeType.ISDBS;
                                        tuningSpec = new TuningSpec((Satellite)satelliteFrequency.Provider, isdbSatelliteFrequency);
                                    }
                                    else
                                    {
                                        ISDBTerrestrialFrequency isdbTerrestrialFrequency = frequency as ISDBTerrestrialFrequency;
                                        if (isdbTerrestrialFrequency != null)
                                        {
                                            tunerNodeType = TunerNodeType.ISDBT;
                                            tuningSpec = new TuningSpec(isdbTerrestrialFrequency);
                                        }
                                        else
                                        {
                                            FileFrequency fileFrequency = frequency as FileFrequency;
                                            if (fileFrequency != null)
                                            {
                                                tunerNodeType = TunerNodeType.Other;
                                                tuningSpec = new TuningSpec();
                                            }
                                            else
                                            {
                                                StreamFrequency streamFrequency = frequency as StreamFrequency;
                                                if (streamFrequency != null)
                                                {
                                                    tunerNodeType = TunerNodeType.Other;
                                                    tuningSpec = new TuningSpec();
                                                }
                                                else
                                                    throw (new InvalidOperationException("Tuning frequency not recognized"));
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                Tuner currentTuner = null;
                bool finished = false;

                while (!finished)
                {
                    if ((sender as BackgroundWorker).CancellationPending)
                    {
                        Logger.Instance.Write("Scan abandoned by user");
                        e.Cancel = true;
                        resetEvent.Set();
                        return;
                    }

                    if (frequency.TunerType != TunerType.File &&
                        frequency.TunerType != TunerType.Stream)
                    {
                        ITunerDataProvider graph = BDAGraph.FindTuner(frequency.SelectedTuners, tunerNodeType, tuningSpec, currentTuner);
                        if (graph == null)
                        {
                            graph = SatIpController.FindReceiver(frequency.SelectedTuners, tunerNodeType, tuningSpec, currentTuner, getDiseqcSetting(tuningSpec.Frequency));
                            if (graph == null)
                            {
                                graph = VBoxController.FindReceiver(frequency.SelectedTuners, tunerNodeType, tuningSpec, currentTuner, getDiseqcSetting(tuningSpec.Frequency), false);
                                if (graph == null)
                                {
                                    Logger.Instance.Write("<e> No tuner able to tune frequency " + frequency.ToString());

                                    if (!ignoreTuningErrors)
                                    {
                                        if (frequencies.IndexOf(frequency) != frequencies.Count - 1)
                                        {
                                            Logger.Instance.Write("Asking user whether to continue");

                                            DialogResult result = (DialogResult)dgServices.Invoke(new ShowMessage(showMessage), "No tuner able to tune frequency " + frequency.ToString() +
                                                Environment.NewLine + Environment.NewLine + "Do you want to continue scanning the other frequencies?",
                                                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                                            if (result == DialogResult.No)
                                            {
                                                Logger.Instance.Write("User cancelled scan");
                                                e.Cancel = true;
                                                resetEvent.Set();
                                                return;
                                            }
                                            else
                                            {
                                                Logger.Instance.Write("Scan continuing");
                                                finished = true;
                                            }
                                        }
                                        else
                                        {
                                            dgServices.Invoke(new ShowMessage(showMessage), "No tuner able to tune frequency " + frequency.ToString(),
                                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                                            finished = true;
                                        }
                                    }
                                    else
                                    {
                                        Logger.Instance.Write("Scan continuing - tuning failure ignored as requested by user");
                                        finished = true;
                                    }
                                }
                            }
                        }

                        if (!finished)
                        {
                            string tuneReply = checkTuning(graph, frequency, sender as BackgroundWorker);

                            if ((sender as BackgroundWorker).CancellationPending)
                            {
                                Logger.Instance.Write("Scan abandoned by user");
                                graph.Dispose();
                                e.Cancel = true;
                                resetEvent.Set();
                                return;
                            }

                            if (tuneReply == null)
                            {
                                getStations((ISampleDataProvider)graph, frequency, sender as BackgroundWorker);
                                graph.Dispose();
                                finished = true;
                            }
                            else
                            {
                                Logger.Instance.Write("Failed to tune frequency " + frequency.ToString());
                                graph.Dispose();
                                currentTuner = graph.Tuner;
                            }
                        }
                    }
                    else
                    {
                        if (frequency as FileFrequency != null)
                        {
                            string tsFileName = ((FileFrequency)frequency).Path;

                            SimulationDataProvider dataProvider = new SimulationDataProvider(tsFileName, frequency);
                            string providerReply = dataProvider.Run();
                            if (providerReply != null)
                            {
                                dgServices.Invoke(new ShowMessage(showMessage), "Simulation file failure - " + tsFileName,
                                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                                finished = true;
                            }
                            else
                            {
                                getStations(dataProvider, frequency, sender as BackgroundWorker);
                                dataProvider.Stop();
                                finished = true;
                            }
                        }
                        else
                        {
                            StreamFrequency streamFrequency = frequency as StreamFrequency;
                            StreamController streamController = new StreamController(streamFrequency.IPAddress, streamFrequency.PortNumber);
                            ErrorSpec errorSpec = streamController.Run(streamFrequency, null);
                            if (errorSpec != null)
                            {
                                Logger.Instance.Write("<e> Stream Data Provider failed");
                                Logger.Instance.Write("<e> " + errorSpec);

                                dgServices.Invoke(new ShowMessage(showMessage), "Stream input failed." +
                                    Environment.NewLine + Environment.NewLine + errorSpec,
                                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                                finished = true;
                            }
                            else
                            {
                                getStations(streamController, frequency, sender as BackgroundWorker);
                                streamController.Stop();
                                finished = true;
                            }
                        }
                    }
                }
            }

            e.Cancel = true;
            resetEvent.Set();
        }

        private static int getDiseqcSetting(TuningFrequency frequency)
        {
            SatelliteFrequency satelliteFrequency = frequency as SatelliteFrequency;
            if (satelliteFrequency == null)
                return (0);

            if (satelliteFrequency.DiseqcRunParamters.DiseqcSwitch == null)
                return (0);

            switch (satelliteFrequency.DiseqcRunParamters.DiseqcSwitch)
            {
                case "A":
                    return (1);
                case "B":
                    return (2);
                case "AA":
                    return (1);
                case "AB":
                    return (2);
                case "BA":
                    return (3);
                case "BB":
                    return (4);
                default:
                    return (0);
            }
        }

        private DialogResult showMessage(string message, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            lblScanning.Visible = false;
            pbarChannels.Visible = false;
            pbarChannels.Enabled = false;

            lblPlusScanning.Visible = false;
            pbarPlusScan.Enabled = false;
            pbarPlusScan.Visible = false;

            DialogResult result = MessageBox.Show(message, "EPG Centre", buttons, icon);
            if (result == DialogResult.Yes)
            {
                lblScanning.Visible = true;
                pbarChannels.Visible = true;
                pbarChannels.Enabled = true;

                lblPlusScanning.Visible = true;
                pbarPlusScan.Enabled = true;
                pbarPlusScan.Visible = true;
            }

            return (result);
        }

        private string checkTuning(ITunerDataProvider graph, TuningFrequency frequency, BackgroundWorker worker)
        {
            TimeSpan timeout = new TimeSpan();
            bool done = false;
            bool locked = false;
            int frequencyRetries = 0;

            while (!done)
            {
                if (worker.CancellationPending)
                {
                    Logger.Instance.Write("Scan abandoned by user");
                    return (null);
                }

                locked = graph.SignalLocked;
                if (!locked)
                {
                    if (graph.SignalQuality > 0)
                    {
                        locked = true;
                        done = true;
                    }
                    else
                    {
                        if (graph.SignalPresent)
                        {
                            locked = true;
                            done = true;
                        }
                        else
                        {
                            Logger.Instance.Write("Signal not acquired: lock is " + graph.SignalLocked + " quality is " + graph.SignalQuality + " signal not present");
                            Thread.Sleep(1000);
                            timeout = timeout.Add(new TimeSpan(0, 0, 1));
                            done = (timeout.TotalSeconds == runParameters.LockTimeout.TotalSeconds);
                        }
                    }

                    if (done)
                    {
                        done = (frequencyRetries == 2);
                        if (done)
                            Logger.Instance.Write("<e> Failed to acquire signal");
                        else
                        {
                            Logger.Instance.Write("Retrying frequency");
                            timeout = new TimeSpan();
                            frequencyRetries++;
                        }
                    }
                }
                else
                {
                    Logger.Instance.Write("Signal acquired: lock is " + graph.SignalLocked + " quality is " + graph.SignalQuality + " strength is " + graph.SignalStrength);
                    done = true;
                }
            }

            if (locked)
                return (null);
            else
                return ("<e> The tuner failed to acquire a signal for frequency " + frequency.ToString());
        }

        private bool getStations(ISampleDataProvider graph, TuningFrequency frequency, BackgroundWorker worker)
        {
            FrequencyScanner frequencyScanner;

            if (frequency.CollectionType != CollectionType.FreeSat)
                frequencyScanner = new FrequencyScanner(graph, worker, true);
            else
                frequencyScanner = new FrequencyScanner(graph, new int[] { 0xbba }, true, worker);

            Collection<TVStation> stations = frequencyScanner.FindTVStations();

            int addedCount = 0;

            if (stations != null)
            {
                foreach (TVStation tvStation in stations)
                {
                    TVStation existingStation = TVStation.FindStation(runParameters.StationCollection,
                        tvStation.OriginalNetworkID, tvStation.TransportStreamID, tvStation.ServiceID);
                    if (existingStation == null)
                    {
                        tvStation.CollectionType = frequency.CollectionType;
                        bool added = TVStation.AddStation(runParameters.StationCollection, tvStation);
                        if (added)
                        {
                            Logger.Instance.Write("Included station: " + tvStation.FixedLengthName + " (" + tvStation.FullID + " Service type " + tvStation.ServiceType + ")");
                            addedCount++;
                        }
                    }
                    else
                        existingStation.Name = tvStation.Name;
                }

                Logger.Instance.Write("Added " + addedCount + " stations for frequency " + frequency);
            }

            return (true);
        }

        private void scanStationsRunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            cmdScan.Text = "Start Scan";
            btPlusScan.Text = "Start Scan";

            if (e.Error != null)
                throw new InvalidOperationException("Background worker failed - see inner exception", e.Error);

            lblScanning.Visible = false;
            pbarChannels.Visible = false;
            pbarChannels.Enabled = false;

            lblPlusScanning.Visible = false;
            pbarPlusScan.Enabled = false;
            pbarPlusScan.Visible = false;

            MainWindow.ChangeMenuItemAvailability(true);

            populateServicesGrid();
            populatePlusChannels(lbPlusSourceChannel);
            populatePlusChannels(lbPlusDestinationChannel);
        }

        private void populateServicesGrid()
        {
            if (runParameters.StationCollection.Count != 0)
            {
                Collection<TVStation> sortedStations = new Collection<TVStation>();

                foreach (TVStation station in runParameters.StationCollection)
                    addInOrder(sortedStations, station, sortedAscending, sortedKeyName);

                bindingList = new BindingList<TVStation>();
                foreach (TVStation station in sortedStations)
                    bindingList.Add(station);

                tvStationBindingSource.DataSource = bindingList;

                dgServices.DataSource = tvStationBindingSource;
                cmdSelectAll.Enabled = true;
                cmdSelectNone.Enabled = true;
                cmdClearScan.Enabled = true;

                MessageBox.Show("The scan for channels is complete." + Environment.NewLine + Environment.NewLine +
                    "There are now " + bindingList.Count + " channels in the list.", "EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                cmdScan.Enabled = true;
                cmdSelectAll.Enabled = false;
                cmdSelectNone.Enabled = false;
                cmdClearScan.Enabled = false;
            }
        }

        private void tmrProgressTick(object sender, EventArgs e)
        {

        }

        private void cmdIncludeAll_Click(object sender, EventArgs e)
        {
            foreach (TVStation station in bindingList)
                station.ExcludedByUser = false;

            foreach (DataGridViewRow row in dgServices.Rows)
            {
                foreach (DataGridViewCell cell in row.Cells)
                {
                    cell.Style.ForeColor = Color.Black;
                    cell.Style.SelectionForeColor = Color.White;
                }
            }
        }

        private void cmdExcludeAll_Click(object sender, EventArgs e)
        {
            foreach (TVStation station in bindingList)
                station.ExcludedByUser = true;

            foreach (DataGridViewRow row in dgServices.Rows)
            {
                foreach (DataGridViewCell cell in row.Cells)
                {
                    cell.Style.ForeColor = Color.Red;
                    cell.Style.SelectionForeColor = Color.Red;
                }
            }
        }

        private void populatePlusChannels(ListBox listBox)
        {
            listBox.Items.Clear();

            foreach (TVStation station in runParameters.StationCollection)
                listBox.Items.Add(station);
        }

        private void btPlusAdd_Click(object sender, EventArgs e)
        {
            if ((TVStation)lbPlusSourceChannel.SelectedItem == (TVStation)lbPlusDestinationChannel.SelectedItem)
            {
                showErrorMessage("The source and destination channels must be different");
                return;
            }

            TimeOffsetChannel newChannel = new TimeOffsetChannel((TVStation)lbPlusSourceChannel.SelectedItem,
                (TVStation)lbPlusDestinationChannel.SelectedItem,
                (int)nudPlusIncrement.Value);

            ListViewItem newItem = new ListViewItem(newChannel.SourceChannel.Name);
            newItem.Tag = newChannel;
            newItem.SubItems.Add(newChannel.DestinationChannel.Name);
            newItem.SubItems.Add(newChannel.Offset.ToString());

            foreach (ListViewItem oldItem in lvPlusSelectedChannels.Items)
            {
                int index = lvPlusSelectedChannels.Items.IndexOf(oldItem);

                TimeOffsetChannel oldChannel = oldItem.Tag as TimeOffsetChannel;

                if (oldChannel.SourceChannel.Name == newChannel.SourceChannel.Name &&
                    oldChannel.DestinationChannel.Name == newChannel.DestinationChannel.Name)
                {
                    if (oldChannel.Offset == newChannel.Offset)
                    {
                        MessageBox.Show("The channels have already been selected with the same offset.",
                            "EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    else
                    {
                        DialogResult result = MessageBox.Show("The channels have already been selected with a different offset." + Environment.NewLine + Environment.NewLine +
                            "Do you want to overwrite the existing entry?", "EPG Centre", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                        switch (result)
                        {
                            case DialogResult.Yes:
                                lvPlusSelectedChannels.Items.Remove(oldItem);
                                lvPlusSelectedChannels.Items.Insert(index, newItem);
                                return;
                            default:
                                return;
                        }
                    }
                }
            }

            lvPlusSelectedChannels.Items.Add(newItem);
        }

        private void lvPlusSelectedChannels_SelectedIndexChanged(object sender, EventArgs e)
        {
            btPlusDelete.Enabled = (lvPlusSelectedChannels.SelectedItems.Count != 0);
        }

        private void btPlusDelete_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem item in lvPlusSelectedChannels.SelectedItems)
                lvPlusSelectedChannels.Items.Remove(item);

            btPlusDelete.Enabled = (lvPlusSelectedChannels.SelectedItems.Count != 0);
        }

        private void btExcludeAdd_Click(object sender, EventArgs e)
        {
            int frequency = -1;
            int originalNetworkID = -1;
            int transportStreamID = -1;
            int startServiceID = -1;
            int endServiceID = -1;

            if (cboFilterFrequency.SelectedIndex > -1 && cboFilterFrequency.SelectedIndex < cboFilterFrequency.Items.Count)
                frequency = ((TuningFrequency)cboFilterFrequency.Items[cboFilterFrequency.SelectedIndex]).Frequency;

            if (tbExcludeONID.Text.Length != 0)
            {
                try
                {
                    originalNetworkID = Int32.Parse(tbExcludeONID.Text.Trim());
                }
                catch (FormatException)
                {
                    MessageBox.Show("The original network ID is incorrect.",
                        "EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                catch (OverflowException)
                {
                    MessageBox.Show("The original network ID is incorrect.",
                        "EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            if (tbExcludeTSID.Text.Length != 0)
            {
                try
                {
                    transportStreamID = Int32.Parse(tbExcludeTSID.Text.Trim());
                }
                catch (FormatException)
                {
                    MessageBox.Show("The transport stream ID is incorrect.",
                        "EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                catch (OverflowException)
                {
                    MessageBox.Show("The transport stream ID is incorrect.",
                        "EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            if (tbExcludeSIDStart.Text.Length != 0)
            {
                try
                {
                    startServiceID = Int32.Parse(tbExcludeSIDStart.Text.Trim());
                }
                catch (FormatException)
                {
                    MessageBox.Show("The start service ID is incorrect.",
                        "EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                catch (OverflowException)
                {
                    MessageBox.Show("The start service ID is incorrect.",
                        "EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            if (tbExcludeSIDEnd.Text.Length != 0)
            {
                try
                {
                    endServiceID = Int32.Parse(tbExcludeSIDEnd.Text.Trim());
                }
                catch (FormatException)
                {
                    MessageBox.Show("The end service ID is incorrect.",
                        "EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                catch (OverflowException)
                {
                    MessageBox.Show("The end service ID is incorrect.",
                        "EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            if (originalNetworkID == -1 && transportStreamID == -1 && startServiceID == -1 && endServiceID == -1)
            {
                MessageBox.Show("No filter entered.",
                    "EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (startServiceID == -1 && endServiceID != -1)
            {
                MessageBox.Show("The start service ID cannot be omitted if an end service ID is entered.",
                    "EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            ChannelFilterEntry newFilter = new ChannelFilterEntry(frequency, originalNetworkID, transportStreamID, startServiceID, endServiceID);

            ListViewItem newItem = null;

            newItem = new ListViewItem(frequency.ToString());

            newItem.Tag = newFilter;

            if (originalNetworkID != -1)
                newItem.SubItems.Add(originalNetworkID.ToString());
            else
                newItem.SubItems.Add("");

            if (transportStreamID != -1)
                newItem.SubItems.Add(transportStreamID.ToString());
            else
                newItem.SubItems.Add("");

            if (startServiceID != -1)
                newItem.SubItems.Add(startServiceID.ToString());
            else
                newItem.SubItems.Add("");

            if (endServiceID != -1)
                newItem.SubItems.Add(endServiceID.ToString());
            else
                newItem.SubItems.Add("");

            foreach (ListViewItem oldItem in lvExcludedIdentifiers.Items)
            {
                int index = lvExcludedIdentifiers.Items.IndexOf(oldItem);

                ChannelFilterEntry oldFilter = oldItem.Tag as ChannelFilterEntry;

                if (oldFilter.Frequency == newFilter.Frequency &&
                    oldFilter.OriginalNetworkID == newFilter.OriginalNetworkID &&
                    oldFilter.TransportStreamID == newFilter.TransportStreamID &&
                    oldFilter.StartServiceID == newFilter.StartServiceID &&
                    oldFilter.EndServiceID == newFilter.EndServiceID)
                {
                    MessageBox.Show("A filter has already been created with the same parameters.",
                        "EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            lvExcludedIdentifiers.Items.Add(newItem);
        }

        private void btExcludeDelete_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem item in lvExcludedIdentifiers.SelectedItems)
                lvExcludedIdentifiers.Items.Remove(item);

            btExcludeDelete.Enabled = (lvExcludedIdentifiers.SelectedItems.Count != 0);
        }

        private void lvExcludedIdentifiers_SelectedIndexChanged(object sender, EventArgs e)
        {
            btExcludeDelete.Enabled = (lvExcludedIdentifiers.SelectedItems.Count != 0);
        }

        private void cbCheckForRepeats_CheckedChanged(object sender, EventArgs e)
        {
            if (cboWMCSeries.SelectedIndex != 0 && cbCheckForRepeats.Checked)
            {
                MessageBox.Show("EPG Collector repeat checking and Windows Media Center series/repeat checking cannot be enabled at the same time." +
                    Environment.NewLine + Environment.NewLine +
                    "Windows Media Center repeat checking will be disabled.",
                    "EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                cboWMCSeries.SelectedIndex = 0;
            }

            cbNoSimulcastRepeats.Enabled = cbCheckForRepeats.Checked;
            if (!cbNoSimulcastRepeats.Enabled)
                cbNoSimulcastRepeats.Checked = false;

            cbIgnoreWMCRecordings.Enabled = cbCheckForRepeats.Checked;
            if (!cbIgnoreWMCRecordings.Enabled)
                cbIgnoreWMCRecordings.Checked = false;

            gpRepeatExclusions.Enabled = cbCheckForRepeats.Checked;
            if (!gpRepeatExclusions.Enabled)
            {
                lvRepeatPrograms.Items.Clear();
                tbPhrasesToIgnore.Text = null;
            }
        }

        private void tbRepeatTitle_TextChanged(object sender, EventArgs e)
        {
            btRepeatAdd.Enabled = tbRepeatTitle.Text.Length != 0 || tbRepeatDescription.Text.Length != 0;
        }

        private void tbRepeatDescription_TextChanged(object sender, EventArgs e)
        {
            btRepeatAdd.Enabled = tbRepeatTitle.Text.Length != 0 || tbRepeatDescription.Text.Length != 0;
        }

        private void btRepeatAdd_Click(object sender, EventArgs e)
        {
            ListViewItem newItem = new ListViewItem(tbRepeatTitle.Text);
            newItem.SubItems.Add(tbRepeatDescription.Text);
            lvRepeatPrograms.Items.Add(newItem);

            tbRepeatTitle.Text = string.Empty;
            tbRepeatDescription.Text = string.Empty;
        }

        private void lvRepeatPrograms_SelectedIndexChanged(object sender, EventArgs e)
        {
            btRepeatDelete.Enabled = lvRepeatPrograms.Items.Count != 0;
        }

        private void btRepeatDelete_Click(object sender, EventArgs e)
        {
            lvRepeatPrograms.Items.Remove(lvRepeatPrograms.SelectedItems[0]);
            btRepeatDelete.Enabled = lvRepeatPrograms.Items.Count != 0;
        }

        private void cbMovieLookupEnabled_CheckedChanged(object sender, EventArgs e)
        {
            gpMovieLookup.Enabled = cbMovieLookupEnabled.Checked;
            gpLookupMisc.Enabled = gpMovieLookup.Enabled || gpTVLookup.Enabled;
        }

        private void cbTVLookupEnabled_CheckedChanged(object sender, EventArgs e)
        {
            gpTVLookup.Enabled = cbTVLookupEnabled.Checked;
            gpLookupMisc.Enabled = gpMovieLookup.Enabled || gpTVLookup.Enabled;
        }

        private void btTimeoutDefaults_Click(object sender, EventArgs e)
        {
            nudSignalLockTimeout.Value = timeoutLock;
            nudDataCollectionTimeout.Value = timeoutCollection;
            nudScanRetries.Value = timeoutRetries;
            nudBufferSize.Value = bufferSize;
            nudBufferFills.Value = bufferFills;
        }

        private void cbManualTime_CheckedChanged(object sender, EventArgs e)
        {
            gpTimeAdjustments.Enabled = cbManualTime.Checked;

            if (!gpTimeAdjustments.Enabled)
            {
                nudCurrentOffsetHours.Value = 0;
                nudCurrentOffsetMinutes.Value = 0;
                nudNextOffsetHours.Value = 0;
                nudNextOffsetMinutes.Value = 0;
                tbChangeDate.Text = string.Empty;
                nudChangeHours.Value = 0;
                nudChangeMinutes.Value = 0;
            }
        }

        private void cboDiseqcHandler_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (DiseqcHandlerBase.IsGeneric(cboDiseqcHandler.Text))
            {
                cbDisableDriverDiseqc.Enabled = true;
                cbUseDiseqcCommands.Enabled = true;
            }
            else
            {
                cbDisableDriverDiseqc.Enabled = false;
                cbDisableDriverDiseqc.Checked = false;
                cbUseDiseqcCommands.Enabled = false;
                cbUseDiseqcCommands.Checked = false;
            }
        }

        private void btLookupChangeNotMovie_Click(object sender, EventArgs e)
        {
            Collection<string> currentList = new Collection<string>();

            foreach (string entry in cboLookupNotMovie.Items)
                currentList.Add(entry);

            ChangeNotMovieList changeNotMovieList = new ChangeNotMovieList(currentList);
            DialogResult result = changeNotMovieList.ShowDialog();
            if (result == DialogResult.Cancel)
                return;

            cboLookupNotMovie.Items.Clear();

            foreach (string entry in currentList)
                cboLookupNotMovie.Items.Add(entry);

            if (cboLookupNotMovie.Items.Count != 0)
                cboLookupNotMovie.SelectedIndex = 0;
        }

        private void btLookupBaseBrowse_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog browsePath = new FolderBrowserDialog();
            browsePath.Description = "EPG Centre - Find Lookup Image Base Directory";
            if (currentLookupBasePath == null)
                browsePath.SelectedPath = RunParameters.DataDirectory;
            else
                browsePath.SelectedPath = currentLookupBasePath;
            DialogResult result = browsePath.ShowDialog();
            if (result == DialogResult.Cancel)
                return;

            currentLookupBasePath = browsePath.SelectedPath;
            tbLookupImagePath.Text = browsePath.SelectedPath;
        }

        private void cbDVBLinkUpdateEnabled_CheckedChanged(object sender, EventArgs e)
        {
            gpDVBLink.Enabled = cbDVBLinkUpdateEnabled.Checked;
        }

        private void cbWMCUpdateEnabled_CheckedChanged(object sender, EventArgs e)
        {
            gpDVBLink.Enabled = !gpWMCOptions.Enabled;
        }

        private void tbXmltvPath_TextChanged(object sender, EventArgs e)
        {
            btXmltvAdd.Enabled = tbXmltvPath.Text.Trim().Length != 0;

            if (!tbXmltvPath.Text.Trim().ToLowerInvariant().EndsWith("mxf"))
            {
                cboXmltvLanguage.Enabled = true;
                cboXmltvIdFormat.Enabled = true;
            }
            else
            {
                cboXmltvLanguage.Enabled = false;
                cboXmltvLanguage.SelectedIndex = 0;
                cboXmltvIdFormat.Enabled = false;
                cboXmltvIdFormat.SelectedIndex = 0;
            }
        }

        private void btXmltvBrowse_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFile = new OpenFileDialog();
            openFile.Filter = "XML Files (*.xml, *.mxf)|*.xml;*.mxf";
            if (currentXmltvImportPath == null)
                openFile.InitialDirectory = RunParameters.DataDirectory;
            else
                openFile.InitialDirectory = currentXmltvImportPath;
            openFile.RestoreDirectory = true;
            openFile.Title = "Open XMLTV Import File";

            DialogResult result = openFile.ShowDialog();
            if (result == DialogResult.Cancel)
                return;

            currentXmltvImportPath = new FileInfo(openFile.FileName).DirectoryName;
            tbXmltvPath.Text = openFile.FileName;
        }

        private void btXmltvAdd_Click(object sender, EventArgs e)
        {
            try
            {
                Uri checkPath = new Uri(tbXmltvPath.Text);
            }
            catch (UriFormatException)
            {
                MessageBox.Show("The file name is in the wrong format.",
                    "EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            ImportFileSpec xmltvFileSpec = new ImportFileSpec(tbXmltvPath.Text);
            xmltvFileSpec.Precedence = (DataPrecedence)Enum.Parse(typeof(DataPrecedence), cboXmltvPrecedence.Text);

            if (cboXmltvLanguage.SelectedIndex != 0)
                xmltvFileSpec.Language = (LanguageCode)cboXmltvLanguage.SelectedItem;

            switch (cboXmltvIdFormat.SelectedIndex)
            {
                case 0:
                    xmltvFileSpec.IdFormat = XmltvIdFormat.Undefined;
                    break;
                case 1:
                    xmltvFileSpec.IdFormat = XmltvIdFormat.ServiceId;
                    break;
                case 2:
                    xmltvFileSpec.IdFormat = XmltvIdFormat.UserChannelNumber;
                    break;
                case 3:
                    xmltvFileSpec.IdFormat = XmltvIdFormat.FullChannelId;
                    break;
                case 4:
                    xmltvFileSpec.IdFormat = XmltvIdFormat.Name;
                    break;
                default:
                    xmltvFileSpec.IdFormat = XmltvIdFormat.Undefined;
                    break;
            }

            xmltvFileSpec.NoLookup = cbXmltvNoLookup.Checked;
            xmltvFileSpec.AppendOnly = cbXmltvAppendOnly.Checked;
            if (cboXmltvTimeZone.SelectedIndex != 0)
                xmltvFileSpec.TimeZone = cboXmltvTimeZone.Text;

            ListViewItem item = new ListViewItem(xmltvFileSpec.FileName);
            item.Tag = xmltvFileSpec;

            if (xmltvFileSpec.Language != null)
                item.SubItems.Add(xmltvFileSpec.Language.Description);
            else
                item.SubItems.Add(string.Empty);

            item.SubItems.Add(xmltvFileSpec.Precedence.ToString());
            item.SubItems.Add(xmltvFileSpec.NoLookup ? "Yes" : "No");
            item.SubItems.Add(xmltvFileSpec.AppendOnly ? "Yes" : "No");
            item.SubItems.Add(xmltvFileSpec.TimeZone == null ? "Local" : xmltvFileSpec.TimeZone);

            lvXmltvSelectedFiles.Items.Add(item);

            tbXmltvPath.Text = string.Empty;
            btXmltvLoadFiles.Enabled = true;
        }

        private void lvXmltvSelectedFiles_SelectedIndexChanged(object sender, EventArgs e)
        {
            btXmltvDelete.Enabled = lvXmltvSelectedFiles.SelectedItems.Count != 0;
            btXmltvLoadFiles.Enabled = lvXmltvSelectedFiles.SelectedItems.Count != 0;
        }

        private void btXmltvDelete_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem item in lvXmltvSelectedFiles.SelectedItems)
                lvXmltvSelectedFiles.Items.Remove(item);

            btXmltvDelete.Enabled = lvXmltvSelectedFiles.SelectedItems.Count != 0;
            btXmltvLoadFiles.Enabled = lvXmltvSelectedFiles.SelectedItems.Count != 0;
        }

        private void btXmltvLoadFiles_Click(object sender, EventArgs e)
        {
            Cursor.Current = Cursors.WaitCursor;

            XmltvController.Clear();
            MxfController.Clear();

            foreach (ListViewItem item in lvXmltvSelectedFiles.Items)
            {
                ImportFileSpec fileSpec = item.Tag as ImportFileSpec;

                string actualName = ImportFileBase.GetActualFileName(fileSpec.FileName);
                ImportFileBase importFileController;

                if (!actualName.ToLowerInvariant().EndsWith("mxf"))
                    importFileController = new XmltvController();
                else
                    importFileController = new MxfController();

                string reply = importFileController.ProcessChannels(actualName, fileSpec);
                if (reply != null)
                    MessageBox.Show("The import file '" + fileSpec.FileName + "' could not be loaded." + Environment.NewLine + Environment.NewLine + reply,
                        " EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Information);

                ImportFileBase.DeleteTemporaryFile();
            }

            xmltvChannelBindingList = new BindingList<ImportChannelChange>();

            if (XmltvChannel.Channels != null)
            {
                foreach (XmltvChannel channel in XmltvChannel.Channels)
                    addChannelChange(xmltvChannelBindingList, new ImportChannelChange(channel.DisplayNames[0].Text));
            }

            if (MxfService.Services != null)
            {
                foreach (MxfService service in MxfService.Services)
                    addChannelChange(xmltvChannelBindingList, new ImportChannelChange(service.Name));
            }

            xmltvChannelChangeBindingSource.DataSource = xmltvChannelBindingList;
            dgXmltvChannelChanges.DataSource = xmltvChannelChangeBindingSource;

            btXmltvClear.Enabled = true;
            btXmltvIncludeAll.Enabled = true;
            btXmltvExcludeAll.Enabled = true;

            Cursor.Current = Cursors.Default;
        }

        private void btXmltvClear_Click(object sender, EventArgs e)
        {
            xmltvChannelBindingList.Clear();

            btXmltvClear.Enabled = false;
            btXmltvIncludeAll.Enabled = false;
            btXmltvExcludeAll.Enabled = false;
        }

        private void btXmltvIncludeAll_Click(object sender, EventArgs e)
        {
            foreach (ImportChannelChange xmltvChannelChange in xmltvChannelBindingList)
                xmltvChannelChange.Excluded = false;

            foreach (DataGridViewRow row in dgXmltvChannelChanges.Rows)
            {
                foreach (DataGridViewCell cell in row.Cells)
                {
                    cell.Style.ForeColor = Color.Black;
                    cell.Style.SelectionForeColor = Color.White;
                }
            }
        }

        private void btXmltvExcludeAll_Click(object sender, EventArgs e)
        {
            foreach (ImportChannelChange xmltvChannelChange in xmltvChannelBindingList)
                xmltvChannelChange.Excluded = true;

            foreach (DataGridViewRow row in dgXmltvChannelChanges.Rows)
            {
                foreach (DataGridViewCell cell in row.Cells)
                {
                    cell.Style.ForeColor = Color.Red;
                    cell.Style.SelectionForeColor = Color.Red;
                }
            }
        }

        private void addChannelChange(BindingList<ImportChannelChange> channelChanges, ImportChannelChange newChannelChange)
        {
            foreach (ImportChannelChange oldChannelChange in channelChanges)
            {
                if (oldChannelChange.DisplayName.CompareTo(newChannelChange.DisplayName) > 0)
                {
                    channelChanges.Insert(channelChanges.IndexOf(oldChannelChange), newChannelChange);
                    return;
                }
            }

            channelChanges.Add(newChannelChange);
        }

        private void onXmltvCellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (xmltvChannelBindingList[e.RowIndex].Excluded)
                e.CellStyle.ForeColor = Color.Red;
        }

        private void dgXmltvChannelChanges_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (dgXmltvChannelChanges.CurrentCell.ColumnIndex == dgXmltvChannelChanges.Columns["xmltvNewNameColumn"].Index)
            {
                TextBox textEdit = e.Control as TextBox;
                textEdit.KeyPress -= new KeyPressEventHandler(xmltvTextEdit_KeyPressAlphaNumeric);
                textEdit.KeyPress -= new KeyPressEventHandler(xmltvTextEdit_KeyPressNumeric);
                textEdit.KeyPress += new KeyPressEventHandler(xmltvTextEdit_KeyPressAlphaNumeric);
            }
            else
            {
                if (dgXmltvChannelChanges.CurrentCell.ColumnIndex == dgXmltvChannelChanges.Columns["xmltvChannelNumberColumn"].Index)
                {
                    TextBox textEdit = e.Control as TextBox;
                    textEdit.KeyPress -= new KeyPressEventHandler(xmltvTextEdit_KeyPressAlphaNumeric);
                    textEdit.KeyPress -= new KeyPressEventHandler(xmltvTextEdit_KeyPressNumeric);
                    textEdit.KeyPress += new KeyPressEventHandler(xmltvTextEdit_KeyPressNumeric);
                }
            }
        }

        private void xmltvTextEdit_KeyPressAlphaNumeric(object sender, KeyPressEventArgs e)
        {
            Regex alphaNumericPattern = new Regex(@"[a-zA-Z0-9!&*()-+?\s\b]");
            e.Handled = !alphaNumericPattern.IsMatch(e.KeyChar.ToString());
        }

        private void xmltvTextEdit_KeyPressNumeric(object sender, KeyPressEventArgs e)
        {
            if ("0123456789\b".IndexOf(e.KeyChar) == -1)
                e.Handled = true;
        }

        private void dgXmltvChannelChanges_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (dgXmltvChannelChanges.IsCurrentCellDirty && dgXmltvChannelChanges.Columns[dgXmltvChannelChanges.CurrentCell.ColumnIndex].Name == "xmltvExcludedColumn")
                dgXmltvChannelChanges.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }

        private void dgXmltvChannelChanges_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (dgXmltvChannelChanges.Columns[e.ColumnIndex].Name != "xmltvExcludedColumn")
                return;

            if (xmltvChannelBindingList[e.RowIndex].Excluded)
            {
                foreach (DataGridViewCell cell in dgXmltvChannelChanges.Rows[e.RowIndex].Cells)
                {
                    cell.Style.ForeColor = Color.Red;
                    cell.Style.SelectionForeColor = Color.Red;
                }
            }
            else
            {
                foreach (DataGridViewCell cell in dgXmltvChannelChanges.Rows[e.RowIndex].Cells)
                {
                    cell.Style.ForeColor = Color.Black;
                    cell.Style.SelectionForeColor = Color.White;
                }
            }

            dgXmltvChannelChanges.Invalidate();
        }

        private void dgXmltvChannelChanges_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            dgXmltvChannelChanges.EndEdit();

            if (sortedColumnName == null)
            {
                sortedAscending = true;
                sortedColumnName = dgXmltvChannelChanges.Columns[e.ColumnIndex].Name;
            }
            else
            {
                if (sortedColumnName == dgXmltvChannelChanges.Columns[e.ColumnIndex].Name)
                    sortedAscending = !sortedAscending;
                else
                    sortedColumnName = dgXmltvChannelChanges.Columns[e.ColumnIndex].Name;
            }

            Collection<ImportChannelChange> sortedChanges = new Collection<ImportChannelChange>();

            foreach (ImportChannelChange channelChange in xmltvChannelBindingList)
            {
                switch (dgXmltvChannelChanges.Columns[e.ColumnIndex].Name)
                {
                    case "xmltvDisplayNameColumn":
                        addInOrder(sortedChanges, channelChange, sortedAscending, "Name");
                        break;
                    case "xmltvExcludedColumn":
                        addInOrder(sortedChanges, channelChange, sortedAscending, "Excluded");
                        break;
                    case "xmltvNewNameColumn":
                        addInOrder(sortedChanges, channelChange, sortedAscending, "NewName");
                        break;
                    case "xmltvChannelNumberColumn":
                        addInOrder(sortedChanges, channelChange, sortedAscending, "ChannelNumber");
                        break;
                    default:
                        return;
                }
            }

            xmltvChannelBindingList = new BindingList<ImportChannelChange>();
            foreach (ImportChannelChange channelChange in sortedChanges)
                xmltvChannelBindingList.Add(channelChange);

            xmltvChannelChangeBindingSource.DataSource = xmltvChannelBindingList;
        }

        private void addInOrder(Collection<ImportChannelChange> channelChanges, ImportChannelChange newChange, bool sortedAscending, string keyName)
        {
            sortedKeyName = keyName;

            foreach (ImportChannelChange oldChange in channelChanges)
            {
                if (sortedAscending)
                {
                    if (oldChange.CompareForSorting(newChange, keyName) > 0)
                    {
                        channelChanges.Insert(channelChanges.IndexOf(oldChange), newChange);
                        return;
                    }
                }
                else
                {
                    if (oldChange.CompareForSorting(newChange, keyName) < 0)
                    {
                        channelChanges.Insert(channelChanges.IndexOf(oldChange), newChange);
                        return;
                    }
                }
            }

            channelChanges.Add(newChange);
        }

        private void tbEditText_TextChanged(object sender, EventArgs e)
        {
            btEditAdd.Enabled = !string.IsNullOrWhiteSpace(tbEditText.Text);
        }

        private void btEditAdd_Click(object sender, EventArgs e)
        {
            if (tbEditText.Text.Contains(",") || tbEditText.Text.Contains("="))
            {
                MessageBox.Show("The text cannot contain commas or equal signs.",
                    "EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            EditSpec editSpec = new EditSpec(tbEditText.Text, (TextLocation)Enum.Parse(typeof(TextLocation), cboEditLocation.Text, true), tbEditReplacementText.Text);
            editSpec.ApplyToTitles = cboEditApplyTo.SelectedIndex != 1;
            editSpec.ApplyToDescriptions = cboEditApplyTo.SelectedIndex != 0;
            editSpec.ReplacementMode = (TextReplacementMode)cboEditReplaceMode.SelectedIndex;

            ListViewItem item = new ListViewItem(editSpec.Text);
            item.Tag = editSpec;

            if (editSpec.ApplyToTitles)
            {
                if (editSpec.ApplyToDescriptions)
                    item.SubItems.Add("Titles and descriptions");
                else
                    item.SubItems.Add("Titles only");
            }
            else
            {
                if (editSpec.ApplyToDescriptions)
                    item.SubItems.Add("Descriptions only");
            }

            item.SubItems.Add(editSpec.Location.ToString());

            if (editSpec.ReplacementText != null)
                item.SubItems.Add(editSpec.ReplacementText);
            else
                item.SubItems.Add(string.Empty);

            switch (editSpec.ReplacementMode)
            {
                case TextReplacementMode.TextOnly:
                    item.SubItems.Add("Text only");
                    break;
                case TextReplacementMode.TextAndFollowing:
                    item.SubItems.Add("Text and following");
                    break;
                case TextReplacementMode.TextAndPreceeding:
                    item.SubItems.Add("Text and preceeding");
                    break;
                case TextReplacementMode.Everything:
                    item.SubItems.Add("Everything");
                    break;
                default:
                    item.SubItems.Add("Text only");
                    break;
            }

            lvEditSpecs.Items.Add(item);

            tbEditText.Text = null;
            tbEditReplacementText.Text = null;
        }

        private void lvEditSpecs_SelectedIndexChanged(object sender, EventArgs e)
        {
            btEditDelete.Enabled = lvEditSpecs.SelectedItems != null && lvEditSpecs.SelectedItems.Count > 0;
        }

        private void btEditDelete_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem item in lvEditSpecs.SelectedItems)
                lvEditSpecs.Items.Remove(item);

            btEditDelete.Enabled = false;
        }

        private void cbXmltvOutputEnabled_CheckedChanged(object sender, EventArgs e)
        {
            gpXmltvOptions.Enabled = cbXmltvOutputEnabled.Checked;
        }

        private void cbWmcOutputEnabled_CheckedChanged(object sender, EventArgs e)
        {
            gpWMCOptions.Enabled = cbWmcOutputEnabled.Checked;
        }

        private void cbDvbViewerOutputEnabled_CheckedChanged(object sender, EventArgs e)
        {
            gpDVBViewerOptions.Enabled = cbDvbViewerOutputEnabled.Checked;
        }

        private void cboDiseqc_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cboDiseqc.SelectedIndex != 0)
            {
                cboDiseqcHandler.Enabled = true;

                cbUseSafeDiseqc.Enabled = true;
                cbRepeatDiseqc.Enabled = true;
                cbSwitchAfterPlay.Enabled = true;
                cbSwitchAfterTune.Enabled = true;

                if (DiseqcHandlerBase.IsGeneric(cboDiseqcHandler.Text))
                {
                    cbUseDiseqcCommands.Enabled = true;
                    cbDisableDriverDiseqc.Enabled = true;
                }
            }
            else
            {
                cboDiseqcHandler.Enabled = false;
                if (cboDiseqcHandler.Items.Count != 0)
                    cboDiseqcHandler.SelectedIndex = 0;

                cbUseSafeDiseqc.Enabled = false;
                cbUseSafeDiseqc.Checked = false;
                cbRepeatDiseqc.Enabled = false;
                cbRepeatDiseqc.Checked = false;
                cbSwitchAfterPlay.Enabled = false;
                cbSwitchAfterPlay.Checked = false;
                cbSwitchAfterTune.Enabled = false;
                cbSwitchAfterTune.Checked = false;
                cbUseDiseqcCommands.Enabled = false;
                cbUseDiseqcCommands.Checked = false;
                cbDisableDriverDiseqc.Enabled = false;
                cbDisableDriverDiseqc.Checked = false;
            }
        }

        public bool PrepareToSave()
        {
            dgServices.EndEdit();
            dgXmltvChannelChanges.EndEdit();

            bool tuningTabChanged = hasTuningTabChanged();
            if (tuningTabChanged)
                return (false);

            bool reply = validateData();
            if (reply)
                setRunParameters();

            return (reply);
        }

        private bool validateData()
        {
            if (lvSelectedFrequencies.Items.Count == 0)
            {
                if (lvXmltvSelectedFiles.Items.Count == 0)
                {
                    showErrorMessage("No tuning inputs or XMLTV merge files selected.");
                    return (false);
                }
                else
                {
                    DialogResult showReply = MessageBox.Show("No tuning inputs selected." + Environment.NewLine + Environment.NewLine +
                        "Is that correct?",
                        "EPG Centre", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (showReply == DialogResult.No)
                        return (false);
                }
            }

            if (!cbXmltvOutputEnabled.Checked && !cbWmcOutputEnabled.Checked && !cbDvbViewerOutputEnabled.Checked)
            {
                showErrorMessage("No output method enabled.");
                return (false);
            }

            if (cbXmltvOutputEnabled.Checked && string.IsNullOrWhiteSpace(txtOutputFile.Text))
            {
                showErrorMessage("No XMLTV output path specified.");
                return (false);
            }

            if (cbDvbViewerOutputEnabled.Checked && (!cbDVBViewerImport.Checked && !cbUseDVBViewer.Checked && !cbRecordingServiceImport.Checked))
            {
                showErrorMessage("No DVBViewer input option chosen.");
                return (false);
            }

            if (!string.IsNullOrWhiteSpace(tbSageTVSatelliteNumber.Text))
            {
                try
                {
                    Int32.Parse(tbSageTVSatelliteNumber.Text);
                }
                catch (FormatException)
                {
                    showErrorMessage("The SageTV satellite number on the Files tab is incorrect.");
                    return (false);
                }
                catch (OverflowException)
                {
                    showErrorMessage("The SageTV satellite number on the Files tab is incorrect.");
                    return (false);
                }
            }

            if (bindingList != null)
            {
                foreach (TVStation station in bindingList)
                {
                    if (station.ExcludedByUser && (station.NewName != null || (station.NewName != null && station.NewName.Trim() != string.Empty) || station.LogicalChannelNumber != -1))
                    {
                        showErrorMessage("Station " + station.Name + " has been both excluded and updated.");
                        return (false);
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(tbExcludedMaxChannel.Text))
            {
                try
                {
                    Int32.Parse(tbExcludedMaxChannel.Text);
                }
                catch (FormatException)
                {
                    showErrorMessage("The maximum service ID on the Filters tab is incorrect.");
                    return (false);
                }
                catch (OverflowException)
                {
                    showErrorMessage("The maximum channel number on the Filters tab is incorrect.");
                    return (false);
                }
            }

            if (cbManualTime.Checked)
            {
                if (tbChangeDate.Text.Trim().Length != 0)
                {
                    if (tbChangeDate.Text.Trim().Length != 6)
                    {
                        showErrorMessage("The date of change to the next time zone is incorrect (ddmmyy)");
                        return (false);
                    }

                    try
                    {
                        DateTime.ParseExact(tbChangeDate.Text.Trim().Substring(0, 2) + tbChangeDate.Text.Trim().Substring(2, 2) + tbChangeDate.Text.Trim().Substring(4, 2), "ddMMyy", CultureInfo.InvariantCulture);
                    }
                    catch (FormatException)
                    {
                        showErrorMessage("The date of change to the next time zone is incorrect (ddmmyy)");
                        return (false);
                    }
                }

                if (nudCurrentOffsetHours.Value != 0 ||
                    nudCurrentOffsetMinutes.Value != 0 ||
                    nudNextOffsetHours.Value != 0 ||
                    nudNextOffsetMinutes.Value != 0 ||
                    nudChangeHours.Value != 0 ||
                    nudChangeMinutes.Value != 0)
                {
                    if (tbChangeDate.Text.Trim().Length == 0)
                    {
                        showErrorMessage("The time zone change data is incorrect." + Environment.NewLine + Environment.NewLine +
                            "A date of change must be entered.");
                        return (false);
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(tbDebugIDs.Text))
            {
                string[] parts = tbDebugIDs.Text.Trim().Split(new char[] { ',' });

                foreach (string part in parts)
                {
                    DebugEntry debugEntry = DebugEntry.GetInstance(part);
                    if (debugEntry == null)
                    {
                        showErrorMessage("The debug ID '" + part.Trim() + "' is undefined or in the wrong format.");
                        return (false);
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(tbTraceIDs.Text))
            {
                string[] parts = tbTraceIDs.Text.Trim().Split(new char[] { ',' });

                foreach (string part in parts)
                {
                    TraceEntry traceEntry = TraceEntry.GetInstance(part);
                    if (traceEntry == null)
                    {
                        showErrorMessage("The trace ID '" + part.Trim() + "' is undefined or in the wrong format.");
                        return (false);
                    }
                }
            }

            return (true);
        }

        private void showErrorMessage(string message)
        {
            MessageBox.Show(message, "EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void setRunParameters()
        {
            runParameters.Options.Clear();

            setTuningTabData();
            setOutputTabData();
            setFilesTabData();
            setChannelsTabData();
            setTimeShiftTabData();
            setFiltersTabData();
            setRepeatsTabData();
            setAdvancedTabData();
            setLookupTabData();
            setUpdateTabData();
            setXmltvTabData();
            setEditTabData();
            setDiagnosticsTabData();
        }

        private void setTuningTabData()
        {
            runParameters.FrequencyCollection.Clear();
            foreach (ListViewItem item in lvSelectedFrequencies.Items)
                runParameters.FrequencyCollection.Add(item.Tag as TuningFrequency);
        }

        private void setOutputTabData()
        {
            if (cbAllowBreaks.Checked)
                runParameters.Options.Add(new OptionEntry(OptionName.AcceptBreaks));
            if (cbRoundTime.Checked)
                runParameters.Options.Add(new OptionEntry(OptionName.RoundTime));
            if (cbCreateSameData.Checked)
                runParameters.Options.Add(new OptionEntry(OptionName.DuplicateSameChannels));
            if (cbNoLogExcluded.Checked)
                runParameters.Options.Add(new OptionEntry(OptionName.NoLogExcluded));
            if (cbRemoveExtractedData.Checked)
                runParameters.Options.Add(new OptionEntry(OptionName.NoRemoveData));
            if (cbTcRelevantChannels.Checked)
                runParameters.Options.Add(new OptionEntry(OptionName.TcRelevantOnly));
            if (cbAddSeasonEpisodeToDesc.Checked)
                runParameters.Options.Add(new OptionEntry(OptionName.AddSeasonEpisodeToDesc));
            if (cbNoDataNoFile.Checked)
                runParameters.Options.Add(new OptionEntry(OptionName.NoDataNoFile));

            if (cbXmltvOutputEnabled.Checked)
            {
                runParameters.OutputFileName = txtOutputFile.Text;

                switch (cboChannelIDFormat.SelectedIndex)
                {
                    case 0:
                        break;
                    case 1:
                        runParameters.Options.Add(new OptionEntry(OptionName.UseChannelId));
                        break;
                    case 2:
                        runParameters.Options.Add(new OptionEntry(OptionName.ChannelIdSeqNo));
                        break;
                    case 3:
                        runParameters.Options.Add(new OptionEntry(OptionName.ChannelIdFullName));
                        break;
                    default:
                        break;
                }

                switch (cboEpisodeTagFormat.SelectedIndex)
                {
                    case 0:
                        runParameters.Options.Add(new OptionEntry(OptionName.ValidEpisodeTag));
                        break;
                    case 1:
                        runParameters.Options.Add(new OptionEntry(OptionName.UseBsepg));
                        break;
                    case 2:
                        runParameters.Options.Add(new OptionEntry(OptionName.UseRawCrid));
                        break;
                    case 3:
                        runParameters.Options.Add(new OptionEntry(OptionName.UseNumericCrid));
                        break;
                    case 4:
                        runParameters.Options.Add(new OptionEntry(OptionName.VBoxEpisodeTag));
                        break;
                    case 5:
                        runParameters.Options.Add(new OptionEntry(OptionName.NoEpisodeTag));
                        break;
                    default:
                        break;
                }

                if (cbUseLCN.Checked)
                    runParameters.Options.Add(new OptionEntry(OptionName.UseLcn));

                if (cbCreateADTag.Checked)
                    runParameters.Options.Add(new OptionEntry(OptionName.CreateAdTag));

                if (cbElementPerTag.Checked)
                    runParameters.Options.Add(new OptionEntry(OptionName.ElementPerTag));

                if (cbOmitPartNumber.Checked)
                    runParameters.Options.Add(new OptionEntry(OptionName.OmitPartNumber));

                if (cbPrefixDescWithAirDate.Checked)
                    runParameters.Options.Add(new OptionEntry(OptionName.PrefixDescriptionWithAirDate));

                if (cbPrefixSubtitleWithSeasonEpisode.Checked)
                    runParameters.Options.Add(new OptionEntry(OptionName.PrefixSubtitleWithSeasonEpisode));

                if (cbCreatePlexEpisodeNumTag.Checked)
                    runParameters.Options.Add(new OptionEntry(OptionName.CreatePlexEpisodeNumTag));

                if (!string.IsNullOrWhiteSpace(tbChannelLogoPath.Text))
                    runParameters.ChannelLogoPath = tbChannelLogoPath.Text.Trim();
                else
                    runParameters.ChannelLogoPath = null;
            }
            else
                runParameters.OutputFileName = null;

            if (cbWmcOutputEnabled.Checked)
            {
                runParameters.Options.Add(new OptionEntry(OptionName.WmcImport));
                if (!string.IsNullOrWhiteSpace(txtImportName.Text))
                    runParameters.WMCImportName = txtImportName.Text.Trim();
                else
                    runParameters.WMCImportName = null;

                if (cbAutoMapEPG.Checked)
                    runParameters.Options.Add(new OptionEntry(OptionName.AutoMapEpg));
                if (cbWMCFourStarSpecial.Checked)
                    runParameters.Options.Add(new OptionEntry(OptionName.WmcStarSpecial));
                if (cbDisableInbandLoader.Checked)
                    runParameters.Options.Add(new OptionEntry(OptionName.DisableInbandLoader));

                switch (cboWMCSeries.SelectedIndex)
                {
                    case 1:
                        runParameters.Options.Add(new OptionEntry(OptionName.UseWmcRepeatCheck));
                        break;
                    case 2:
                        runParameters.Options.Add(new OptionEntry(OptionName.UseWmcRepeatCheckBroadcast));
                        break;
                    default:
                        break;
                }
            }
            else
                runParameters.WMCImportName = null;

            if (cbDvbViewerOutputEnabled.Checked)
            {
                if (cbUseDVBViewer.Checked)
                    runParameters.Options.Add(new OptionEntry(OptionName.UseDvbViewer));
                if (cbDVBViewerImport.Checked)
                    runParameters.Options.Add(new OptionEntry(OptionName.DvbViewerImport));
                if (cbRecordingServiceImport.Checked)
                    runParameters.Options.Add(new OptionEntry(OptionName.DvbViewerRecSvcImport));
                if (cbDVBViewerSubtitleVisible.Checked)
                    runParameters.Options.Add(new OptionEntry(OptionName.DvbViewerSubtitleVisible));
                if (cbDVBViewerClear.Checked)
                    runParameters.Options.Add(new OptionEntry(OptionName.DvbViewerClear));

                if (cbRecordingServiceImport.Checked)
                {
                    if (!string.IsNullOrWhiteSpace(tbDVBViewerIPAddress.Text))
                        runParameters.DVBViewerIPAddress = tbDVBViewerIPAddress.Text.Trim();
                    runParameters.Options.Add(new OptionEntry(OptionName.DvbViewerRecSvcImport, (int)nudPort.Value));
                }
            }
            else
                runParameters.DVBViewerIPAddress = null;
        }

        private void setFilesTabData()
        {
            if (cbBladeRunnerFile.Checked)
            {
                runParameters.Options.Add(new OptionEntry(OptionName.CreateBrChannels));

                string bladeRunnerName = tbBladeRunnerFileName.Text.Trim();
                if (!string.IsNullOrWhiteSpace(bladeRunnerName))
                    runParameters.BladeRunnerFileName = bladeRunnerName;
                else
                    runParameters.BladeRunnerFileName = null;
            }
            else
                runParameters.BladeRunnerFileName = null;

            if (cbAreaRegionFile.Checked)
            {
                runParameters.Options.Add(new OptionEntry(OptionName.CreateArChannels));

                string areaRegionName = tbAreaRegionFileName.Text.Trim();
                if (!string.IsNullOrWhiteSpace(areaRegionName))
                    runParameters.AreaRegionFileName = areaRegionName;
                else
                    runParameters.AreaRegionFileName = null;
            }
            else
                runParameters.AreaRegionFileName = null;

            if (cbSageTVFile.Checked)
            {
                runParameters.Options.Add(new OptionEntry(OptionName.CreateSageTvFrq));

                string sageTVName = tbSageTVFileName.Text.Trim();
                if (!string.IsNullOrWhiteSpace(sageTVName))
                    runParameters.SageTVFileName = sageTVName;
                else
                    runParameters.SageTVFileName = null;

                if (cbSageTVFileNoEPG.Checked)
                    runParameters.Options.Add(new OptionEntry(OptionName.SageTvOmitNoEpg));

                if (!string.IsNullOrWhiteSpace(tbSageTVSatelliteNumber.Text))
                    runParameters.SageTVSatelliteNumber = Int32.Parse(tbSageTVSatelliteNumber.Text.Trim());
                else
                    runParameters.SageTVSatelliteNumber = -1;
            }
            else
            {
                runParameters.SageTVFileName = null;
                runParameters.SageTVSatelliteNumber = -1;
            }
        }

        private void setChannelsTabData()
        {
            if (bindingList != null)
            {
                foreach (TVStation station in bindingList)
                {
                    TVStation originalStation = TVStation.FindStation(runParameters.StationCollection,
                        station.OriginalNetworkID, station.TransportStreamID, station.ServiceID);
                    if (originalStation != null)
                    {
                        originalStation.ExcludedByUser = station.ExcludedByUser;
                        originalStation.NewName = station.NewName;
                        originalStation.LogicalChannelNumber = station.LogicalChannelNumber;
                    }
                }
            }
        }

        private void setTimeShiftTabData()
        {
            runParameters.TimeOffsetChannels.Clear();

            foreach (ListViewItem timeOffsetItem in lvPlusSelectedChannels.Items)
            {
                TimeOffsetChannel timeOffsetChannel = timeOffsetItem.Tag as TimeOffsetChannel;
                runParameters.TimeOffsetChannels.Add(timeOffsetChannel);
            }
        }

        private void setFiltersTabData()
        {
            runParameters.ChannelFilters.Clear();

            foreach (ListViewItem filterItem in lvExcludedIdentifiers.Items)
            {
                ChannelFilterEntry filterEntry = filterItem.Tag as ChannelFilterEntry;
                runParameters.ChannelFilters.Add(filterEntry);
            }

            if (tbExcludedMaxChannel.Text.Trim().Length != 0)
                runParameters.MaxService = Int32.Parse(tbExcludedMaxChannel.Text.Trim());
            else
                runParameters.MaxService = -1;
        }

        private void setRepeatsTabData()
        {
            if (cbCheckForRepeats.Checked)
            {
                runParameters.Options.Add(new OptionEntry(OptionName.CheckForRepeats));
                if (cbNoSimulcastRepeats.Checked)
                    runParameters.Options.Add(new OptionEntry(OptionName.NoSimulcastRepeats));
                if (cbIgnoreWMCRecordings.Checked)
                    runParameters.Options.Add(new OptionEntry(OptionName.IgnoreWmcRecordings));
            }

            runParameters.Exclusions.Clear();

            foreach (ListViewItem exclusionEntry in lvRepeatPrograms.Items)
            {
                RepeatExclusion exclusion = new RepeatExclusion(exclusionEntry.SubItems[0].Text, exclusionEntry.SubItems[1].Text);
                runParameters.Exclusions.Add(exclusion);
            }

            runParameters.PhrasesToIgnore.Clear();

            if (!string.IsNullOrWhiteSpace(tbPhrasesToIgnore.Text))
            {
                string[] phrases = tbPhrasesToIgnore.Text.Split(new char[] { ',' });

                foreach (string phrase in phrases)
                    runParameters.PhrasesToIgnore.Add(phrase);
            }
        }

        private void setAdvancedTabData()
        {
            if (cbStoreStationInfo.Checked)
                runParameters.Options.Add(new OptionEntry(OptionName.StoreStationInfo));
            if (cbUseStoredStationInfo.Checked)
                runParameters.Options.Add(new OptionEntry(OptionName.UseStoredStationInfo));
            if (cbFromService.Checked)
                runParameters.Options.Add(new OptionEntry(OptionName.RunFromService));

            runParameters.LockTimeout = new TimeSpan((long)(nudSignalLockTimeout.Value * 10000000));
            runParameters.FrequencyTimeout = new TimeSpan((long)(nudDataCollectionTimeout.Value * 10000000));
            runParameters.Repeats = (int)nudScanRetries.Value;
            runParameters.BufferSize = (int)nudBufferSize.Value;
            runParameters.BufferFills = (int)nudBufferFills.Value;

            if (cbManualTime.Checked)
            {
                if (tbChangeDate.Text.Trim() != string.Empty)
                {
                    runParameters.TimeZone = new TimeSpan((int)nudCurrentOffsetHours.Value, (int)nudCurrentOffsetMinutes.Value, 0);
                    runParameters.NextTimeZone = new TimeSpan((int)nudNextOffsetHours.Value, (int)nudNextOffsetMinutes.Value, 0);

                    try
                    {
                        runParameters.NextTimeZoneChange = DateTime.ParseExact(tbChangeDate.Text.Trim().Substring(0, 2) + tbChangeDate.Text.Trim().Substring(2, 2) + tbChangeDate.Text.Trim().Substring(4, 2) + " 000000", "ddMMyy HHmmss", CultureInfo.InvariantCulture) +
                            new TimeSpan((int)nudChangeHours.Value, (int)nudChangeMinutes.Value, 0);
                    }
                    catch (FormatException) { runParameters.NextTimeZoneChange = DateTime.MaxValue; }
                    catch (ArgumentOutOfRangeException) { runParameters.NextTimeZoneChange = DateTime.MaxValue; }

                    runParameters.TimeZoneSet = true;
                }
                else
                    runParameters.TimeZoneSet = false;
            }
            else
            {
                runParameters.TimeZone = new TimeSpan();
                runParameters.NextTimeZone = new TimeSpan();
                runParameters.NextTimeZoneChange = new DateTime();
                runParameters.TimeZoneSet = false;
            }
        }

        private void setLookupTabData()
        {
            runParameters.MovieLookupEnabled = cbMovieLookupEnabled.Checked;

            switch (cboxMovieLookupImageType.SelectedIndex)
            {
                case 0:
                    runParameters.DownloadMovieThumbnail = LookupImageType.Thumbnail;
                    break;
                case 1:
                    runParameters.DownloadMovieThumbnail = LookupImageType.Poster;
                    break;
                case 2:
                    runParameters.DownloadMovieThumbnail = LookupImageType.None;
                    break;
                default:
                    runParameters.DownloadMovieThumbnail = LookupImageType.None;
                    break;
            }

            runParameters.MovieLowTime = (int)nudLookupMovieLowDuration.Value;
            runParameters.MovieHighTime = (int)nudLookupMovieHighDuration.Value;

            runParameters.LookupMoviePhrases.Clear();
            if (tbLookupMoviePhrases.Text != string.Empty)
            {
                runParameters.MoviePhraseSeparator = udMoviePhraseSeparator.Text;

                string[] parts = tbLookupMoviePhrases.Text.Split(new string[] { udMoviePhraseSeparator.Text }, StringSplitOptions.None);

                foreach (string part in parts)
                    runParameters.LookupMoviePhrases.Add(part);
            }

            if (cboLookupNotMovie.Items.Count != 0)
            {
                runParameters.LookupNotMovie = new Collection<string>();
                foreach (string entry in cboLookupNotMovie.Items)
                    runParameters.LookupNotMovie.Add(entry);
            }
            else
                runParameters.LookupNotMovie = null;

            runParameters.TVLookupEnabled = cbTVLookupEnabled.Checked;

            switch (cboxTVLookupImageType.SelectedIndex)
            {
                case 0:
                    runParameters.DownloadTVThumbnail = LookupImageType.Poster;
                    break;
                case 1:
                    runParameters.DownloadTVThumbnail = LookupImageType.Banner;
                    break;
                case 2:
                    runParameters.DownloadTVThumbnail = LookupImageType.Fanart;
                    break;
                case 3:
                    runParameters.DownloadTVThumbnail = LookupImageType.SmallPoster;
                    break;
                case 4:
                    runParameters.DownloadTVThumbnail = LookupImageType.SmallFanart;
                    break;
                case 5:
                    runParameters.DownloadTVThumbnail = LookupImageType.None;
                    break;
                default:
                    runParameters.DownloadTVThumbnail = LookupImageType.None;
                    break;
            }

            runParameters.LookupMatching = (MatchMethod)Enum.Parse(typeof(MatchMethod), cbxLookupMatching.Text);
            runParameters.LookupMatchThreshold = (int)nudLookupMatchThreshold.Value;

            runParameters.LookupNotFound = cbLookupNotFound.Checked;
            runParameters.LookupReload = cbLookupReload.Checked;
            runParameters.LookupIgnoreCategories = cbLookupIgnoreCategories.Checked;
            runParameters.LookupProcessAsTVSeries = cbLookupProcessAsTVSeries.Checked;
            runParameters.LookupTimeLimit = (int)nudLookupTime.Value;
            runParameters.LookupErrorLimit = (int)nudLookupErrors.Value;

            runParameters.LookupIgnoredPhrases.Clear();
            if (tbLookupIgnoredPhrases.Text != string.Empty)
            {
                runParameters.LookupIgnoredPhraseSeparator = udIgnorePhraseSeparator.Text;

                string[] parts = tbLookupIgnoredPhrases.Text.Split(new string[] { udIgnorePhraseSeparator.Text }, StringSplitOptions.None);

                foreach (string part in parts)
                    runParameters.LookupIgnoredPhrases.Add(part);
            }

            if (string.IsNullOrWhiteSpace(tbLookupImagePath.Text))
                runParameters.LookupImagePath = null;
            else
                runParameters.LookupImagePath = tbLookupImagePath.Text.Trim();

            if (string.IsNullOrWhiteSpace(tbLookupXmltvImageTagPath.Text))
                runParameters.LookupXmltvImageTagPath = null;
            else
                runParameters.LookupXmltvImageTagPath = tbLookupXmltvImageTagPath.Text.Trim();

            runParameters.LookupImagesInBase = cbLookupImagesInBase.Checked;
            runParameters.LookupImageNameTitle = cbLookupImageNameTitle.Checked;
        }

        private void setUpdateTabData()
        {
            runParameters.ChannelUpdateEnabled = cbDVBLinkUpdateEnabled.Checked;

            switch (cboMergeMethod.SelectedIndex)
            {
                case 0:
                    runParameters.ChannelMergeMethod = ChannelMergeMethod.None;
                    break;
                case 1:
                    runParameters.ChannelMergeMethod = ChannelMergeMethod.Name;
                    break;
                case 2:
                    runParameters.ChannelMergeMethod = ChannelMergeMethod.Number;
                    break;
                case 3:
                    runParameters.ChannelMergeMethod = ChannelMergeMethod.NameNumber;
                    break;
                default:
                    runParameters.ChannelMergeMethod = ChannelMergeMethod.None;
                    break;
            }

            switch (cboEPGScanner.SelectedIndex)
            {
                case 0:
                    runParameters.ChannelEPGScanner = ChannelEPGScanner.None;
                    break;
                case 1:
                    runParameters.ChannelEPGScanner = ChannelEPGScanner.Default;
                    break;
                case 2:
                    runParameters.ChannelEPGScanner = ChannelEPGScanner.EPGCollector;
                    break;
                case 3:
                    runParameters.ChannelEPGScanner = ChannelEPGScanner.EITScanner;
                    break;
                case 4:
                    runParameters.ChannelEPGScanner = ChannelEPGScanner.Xmltv;
                    break;
                default:
                    runParameters.ChannelEPGScanner = ChannelEPGScanner.None;
                    break;
            }

            runParameters.ChannelChildLock = cbChildLock.Checked;
            runParameters.ChannelLogNetworkMap = cbLogNetworkMap.Checked;
            runParameters.ChannelEPGScanInterval = (int)nudEPGScanInterval.Value;
            runParameters.ChannelReloadData = cbReloadChannelData.Checked;
            runParameters.ChannelUpdateNumber = cbUpdateChannelNumbers.Checked;
            runParameters.ChannelExcludeNew = cbAutoExcludeNew.Checked;
        }

        private void setXmltvTabData()
        {
            if (lvXmltvSelectedFiles.Items.Count != 0)
            {
                runParameters.ImportFiles = new Collection<ImportFileSpec>();
                foreach (ListViewItem item in lvXmltvSelectedFiles.Items)
                    runParameters.ImportFiles.Add(item.Tag as ImportFileSpec);

                if (xmltvChannelBindingList != null)
                {
                    runParameters.ImportChannelChanges = new Collection<ImportChannelChange>();
                    foreach (ImportChannelChange xmltvChannelChange in xmltvChannelBindingList)
                        runParameters.ImportChannelChanges.Add(xmltvChannelChange);
                }
            }
            else
            {
                runParameters.ImportFiles = null;
                runParameters.ImportChannelChanges = null;
            }
        }

        private void setEditTabData()
        {
            if (lvEditSpecs.Items.Count != 0)
            {
                runParameters.EditSpecs = new Collection<EditSpec>();
                foreach (ListViewItem item in lvEditSpecs.Items)
                    runParameters.EditSpecs.Add(item.Tag as EditSpec);
            }
            else
                runParameters.EditSpecs = null;
        }

        private void setDiagnosticsTabData()
        {
            runParameters.DebugIDs.Clear();
            if (!string.IsNullOrWhiteSpace(tbDebugIDs.Text))
            {
                string[] parts = tbDebugIDs.Text.Trim().Split(new char[] { ',' });

                foreach (string part in parts)
                {
                    DebugEntry newEntry = DebugEntry.GetInstance(part);
                    if (newEntry != null)
                        runParameters.DebugIDs.Add(newEntry);
                }
            }

            runParameters.TraceIDs.Clear();
            if (!string.IsNullOrWhiteSpace(tbTraceIDs.Text))
            {
                string[] parts = tbTraceIDs.Text.Trim().Split(new char[] { ',' });

                foreach (string part in parts)
                {
                    TraceEntry newEntry = TraceEntry.GetInstance(part);
                    if (newEntry != null)
                        runParameters.TraceIDs.Add(newEntry);
                }
            }
        }

        private bool hasTuningTabChanged()
        {
            if (currentSatelliteFrequency != null)
            {
                SatelliteFrequency satelliteFrequency = (SatelliteFrequency)currentSatelliteFrequency.Clone();
                updateSatelliteFrequency(satelliteFrequency, false);
                if (!satelliteFrequency.EqualTo(currentSatelliteFrequency, EqualityLevel.Entirely))
                {
                    DialogResult result = MessageBox.Show("The DVB-S tuning parameters have not been updated to the selected frequency list." +
                        Environment.NewLine + Environment.NewLine +
                        "Do you want to update them?",
                        "EPG Centre", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (result == DialogResult.Yes)
                    {
                        tbcDeliverySystem.SelectedTab = tbcDeliverySystem.TabPages["tbpSatellite"];
                        return (true);
                    }
                }
            }

            if (currentTerrestrialFrequency != null)
            {
                TerrestrialFrequency terrestrialFrequency = (TerrestrialFrequency)currentTerrestrialFrequency.Clone();
                updateTerrestrialFrequency(terrestrialFrequency);
                if (!terrestrialFrequency.EqualTo(currentTerrestrialFrequency, EqualityLevel.Entirely))
                {
                    DialogResult result = MessageBox.Show("The DVB-T tuning parameters have not been updated to the selected frequency list." +
                        Environment.NewLine + Environment.NewLine +
                        "Do you want to update them?",
                        "EPG Centre", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (result == DialogResult.Yes)
                    {
                        tbcDeliverySystem.SelectedTab = tbcDeliverySystem.TabPages["tbpTerrestrial"];
                        return (true);
                    }
                }
            }

            if (currentCableFrequency != null)
            {
                CableFrequency cableFrequency = (CableFrequency)currentCableFrequency.Clone();
                updateCableFrequency(cableFrequency);
                if (!cableFrequency.EqualTo(currentCableFrequency, EqualityLevel.Entirely))
                {
                    DialogResult result = MessageBox.Show("The DVB-C tuning parameters have not been updated to the selected frequency list." +
                        Environment.NewLine + Environment.NewLine +
                        "Do you want to update them?",
                        "EPG Centre", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (result == DialogResult.Yes)
                    {
                        tbcDeliverySystem.SelectedTab = tbcDeliverySystem.TabPages["tbpCable"];
                        return (true);
                    }
                }
            }

            if (currentAtscFrequency != null)
            {
                AtscFrequency atscFrequency = (AtscFrequency)currentAtscFrequency.Clone();
                updateAtscFrequency(atscFrequency);
                if (!atscFrequency.EqualTo(currentAtscFrequency, EqualityLevel.Entirely))
                {
                    DialogResult result = MessageBox.Show("The ATSC tuning parameters have not been updated to the selected frequency list." +
                        Environment.NewLine + Environment.NewLine +
                        "Do you want to update them?",
                        "EPG Centre", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (result == DialogResult.Yes)
                    {
                        tbcDeliverySystem.SelectedTab = tbcDeliverySystem.TabPages["tbpAtsc"];
                        return (true);
                    }
                }
            }

            if (currentClearQamFrequency != null)
            {
                ClearQamFrequency clearQamFrequency = (ClearQamFrequency)currentClearQamFrequency.Clone();
                updateClearQamFrequency(clearQamFrequency);
                if (!clearQamFrequency.EqualTo(currentClearQamFrequency, EqualityLevel.Entirely))
                {
                    DialogResult result = MessageBox.Show("The Clear QAM tuning parameters have not been updated to the selected frequency list." +
                        Environment.NewLine + Environment.NewLine +
                        "Do you want to update them?",
                        "EPG Centre", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (result == DialogResult.Yes)
                    {
                        tbcDeliverySystem.SelectedTab = tbcDeliverySystem.TabPages["tbpClearQam"];
                        return (true);
                    }
                }
            }

            if (currentISDBSatelliteFrequency != null)
            {
                ISDBSatelliteFrequency isdbSatelliteFrequency = (ISDBSatelliteFrequency)currentISDBSatelliteFrequency.Clone();
                updateISDBSatelliteFrequency(isdbSatelliteFrequency);
                if (!isdbSatelliteFrequency.EqualTo(currentISDBSatelliteFrequency, EqualityLevel.Entirely))
                {
                    DialogResult result = MessageBox.Show("The ISDB-S tuning parameters have not been updated to the selected frequency list." +
                        Environment.NewLine + Environment.NewLine +
                        "Do you want to update them?",
                        "EPG Centre", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (result == DialogResult.Yes)
                    {
                        tbcDeliverySystem.SelectedTab = tbcDeliverySystem.TabPages["tbpISDBSatellite"];
                        return (true);
                    }
                }
            }

            if (currentISDBTerrestrialFrequency != null)
            {
                ISDBTerrestrialFrequency isdbTerrestrialFrequency = (ISDBTerrestrialFrequency)currentISDBTerrestrialFrequency.Clone();
                updateISDBTerrestrialFrequency(isdbTerrestrialFrequency);
                if (!isdbTerrestrialFrequency.EqualTo(currentISDBTerrestrialFrequency, EqualityLevel.Entirely))
                {
                    DialogResult result = MessageBox.Show("The ISDB-T tuning parameters have not been updated to the selected frequency list." +
                        Environment.NewLine + Environment.NewLine +
                        "Do you want to update them?",
                        "EPG Centre", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (result == DialogResult.Yes)
                    {
                        tbcDeliverySystem.SelectedTab = tbcDeliverySystem.TabPages["tbpISDBTerrestrial"];
                        return (true);
                    }
                }
            }

            if (currentFileFrequency != null)
            {
                FileFrequency fileFrequency = (FileFrequency)currentFileFrequency.Clone();
                updateFileFrequency(fileFrequency);
                if (!fileFrequency.EqualTo(currentFileFrequency, EqualityLevel.Entirely))
                {
                    DialogResult result = MessageBox.Show("The File tuning parameters have not been updated to the selected frequency list." +
                        Environment.NewLine + Environment.NewLine +
                        "Do you want to update them?",
                        "EPG Centre", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (result == DialogResult.Yes)
                    {
                        tbcDeliverySystem.SelectedTab = tbcDeliverySystem.TabPages["tbpFile"];
                        return (true);
                    }
                }
            }

            if (currentStreamFrequency != null)
            {
                StreamFrequency streamFrequency = (StreamFrequency)currentStreamFrequency.Clone();
                updateStreamFrequency(streamFrequency, false);
                if (!streamFrequency.EqualTo(currentStreamFrequency, EqualityLevel.Entirely))
                {
                    DialogResult result = MessageBox.Show("The Stream tuning parameters have not been updated to the selected frequency list." +
                        Environment.NewLine + Environment.NewLine +
                        "Do you want to update them?",
                        "EPG Centre", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (result == DialogResult.Yes)
                    {
                        tbcDeliverySystem.SelectedTab = tbcDeliverySystem.TabPages["tbpStream"];
                        return (true);
                    }
                }
            }

            return (false);
        }

        private DataState hasDataChanged()
        {
            dgServices.EndEdit();

            if (hasTuningTabChanged())
                return (DataState.HasErrors);

            setRunParameters();

            if (originalData == null || newFile)
                return (DomainObjects.DataState.Changed);

            return (runParameters.HasDataChanged(originalData));
        }

        /// <summary>
        /// Save the data to the original file.
        /// </summary>
        /// <returns>True if the file has been saved; false otherwise.</returns>
        public bool Save()
        {
            return (Save(currentFileName));
        }

        /// <summary>
        /// Save the current data set to a specified file.
        /// </summary>
        /// <param name="fileName">The name of the file to be saved.</param>
        /// <returns>True if the data has been saved; false otherwise.</returns>
        public bool Save(string fileName)
        {
            Cursor.Current = Cursors.WaitCursor;
            string message = runParameters.Save(fileName);
            Cursor.Current = Cursors.Arrow;

            if (message == null)
            {
                MessageBox.Show("The parameters have been saved to '" + fileName + "'", "EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Information);
                newFile = false;
                originalData = runParameters.Clone();
                currentFileName = fileName;
            }
            else
                MessageBox.Show("An error has occurred while writing the parameters." + Environment.NewLine + Environment.NewLine + message, "EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Error);

            return (message == null);
        }

        private void cbAreaRegionFile_CheckedChanged(object sender, EventArgs e)
        {
            gpAreaRegionFile.Enabled = cbAreaRegionFile.Checked;
        }

        private void btBrowseAreaRegionFile_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog browseFile = new FolderBrowserDialog();
            browseFile.Description = "EPG Centre - Find Area/Region Channel File Directory";
            if (currentAreaChannelOutputPath == null)
                browseFile.SelectedPath = RunParameters.DataDirectory;
            else
                browseFile.SelectedPath = currentAreaChannelOutputPath;
            DialogResult result = browseFile.ShowDialog();
            if (result == DialogResult.Cancel)
                return;

            currentAreaChannelOutputPath = browseFile.SelectedPath + @"\";
            tbAreaRegionFileName.Text = currentAreaChannelOutputPath;
        }

        private void cbBladeRunnerFile_CheckedChanged(object sender, EventArgs e)
        {
            gpBladeRunnerFile.Enabled = cbBladeRunnerFile.Checked;
        }

        private void btBrowseBladeRunnerFile_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog browseFile = new FolderBrowserDialog();
            browseFile.Description = "EPG Centre - Find BladeRunner Channel File Directory";
            if (currentBladeRunnerOutputPath == null)
                browseFile.SelectedPath = RunParameters.DataDirectory;
            else
                browseFile.SelectedPath = currentBladeRunnerOutputPath;
            DialogResult result = browseFile.ShowDialog();
            if (result == DialogResult.Cancel)
                return;

            currentBladeRunnerOutputPath = browseFile.SelectedPath + @"\";
            tbBladeRunnerFileName.Text = currentBladeRunnerOutputPath;
        }

        private void cbSageTVFile_CheckedChanged(object sender, EventArgs e)
        {
            gpSageTVFile.Enabled = cbSageTVFile.Checked;
        }

        private void btBrowseSageTVFile_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog browseFile = new FolderBrowserDialog();
            browseFile.Description = "EPG Centre - Find SageTV Frequency File Directory";
            if (currentSageTVOutputPath == null)
                browseFile.SelectedPath = RunParameters.DataDirectory;
            else
                browseFile.SelectedPath = currentSageTVOutputPath;
            DialogResult result = browseFile.ShowDialog();
            if (result == DialogResult.Cancel)
                return;

            currentSageTVOutputPath = browseFile.SelectedPath + @"\";
            tbSageTVFileName.Text = currentSageTVOutputPath;
        }

        private void FillTunersList(TunerNodeType nodeType, CheckedListBox tunerListBox, TuningFrequency tuningFrequency)
        {
            tunerListBox.Items.Clear();
            tunerListBox.Items.Add("Any available Tuner");

            bool found = false;

            foreach (Tuner tuner in Tuner.TunerCollection)
            {
                if (!tuner.Name.ToUpper().Contains("DVBLINK") && tuner.Supports(nodeType))
                {
                    tunerListBox.Items.Add(tuner);

                    if (tuningFrequency != null && SelectedTuner.Selected(tuningFrequency.SelectedTuners, Tuner.TunerCollection.IndexOf(tuner) + 1))
                    {
                        tunerListBox.SetItemChecked(tunerListBox.Items.Count - 1, true);
                        found = true;
                    }
                }
            }

            if (!found)
                tunerListBox.SetItemChecked(0, true);
        }

        private void clbTuners_SelectedIndexChanged(object sender, EventArgs e)
        {
            changeTunerListBox(clbSatelliteTuners);
        }

        private void clbTerrestrialTuners_SelectedIndexChanged(object sender, EventArgs e)
        {
            changeTunerListBox(clbTerrestrialTuners);
        }

        private void clbCableTuners_SelectedIndexChanged(object sender, EventArgs e)
        {
            changeTunerListBox(clbCableTuners);
        }

        private void clbAtscTuners_SelectedIndexChanged(object sender, EventArgs e)
        {
            changeTunerListBox(clbAtscTuners);
        }

        private void clbClearQamTuners_SelectedIndexChanged(object sender, EventArgs e)
        {
            changeTunerListBox(clbClearQamTuners);
        }

        private void clbISDBSatelliteTuners_SelectedIndexChanged(object sender, EventArgs e)
        {
            changeTunerListBox(clbISDBSatelliteTuners);
        }

        private void clbISDBTerrestrialTuners_SelectedIndexChanged(object sender, EventArgs e)
        {
            changeTunerListBox(clbISDBTerrestrialTuners);
        }

        private void changeTunerListBox(CheckedListBox listBox)
        {
            if (listBox.SelectedIndices.Count == 0)
                return;

            if (listBox.SelectedIndices[0] == 0)
            {
                for (int index = 1; index < listBox.Items.Count; index++)
                    listBox.SetItemChecked(index, false);
            }
            else
                listBox.SetItemChecked(0, false);
        }

        private void setTuner(TuningFrequency tuningFrequency, CheckedListBox listBox)
        {
            tuningFrequency.SelectedTuners.Clear();

            for (int index = 1; index < listBox.Items.Count; index++)
            {
                if (listBox.GetItemChecked(index))
                {
                    Tuner tuner = (Tuner)listBox.Items[index];

                    if (!tuner.IsServerTuner)
                        tuningFrequency.SelectedTuners.Add(new SelectedTuner(Tuner.TunerCollection.IndexOf(tuner) + 1));
                    else
                        tuningFrequency.SelectedTuners.Add(new SelectedTuner(tuner.UniqueIdentity));
                }
            }
        }

        private void cboWMCSeries_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cboWMCSeries.SelectedIndex != 0 && cbCheckForRepeats.Checked)
            {
                MessageBox.Show("Windows Media Center series/repeat checking and EPG Collector repeat checking cannot be enabled at the same time." +
                    Environment.NewLine + Environment.NewLine +
                    "EPG Collector repeat checking will be disabled.",
                    "EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                cbCheckForRepeats.Checked = false;
            }
        }

        private void btBrowseLogoPath_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog browsePath = new FolderBrowserDialog();
            browsePath.Description = "EPG Centre - Find Channel Logo Directory";
            if (currentChannelLogoPath == null)
                browsePath.SelectedPath = Path.Combine(RunParameters.DataDirectory, "Images") + Path.DirectorySeparatorChar;
            else
                browsePath.SelectedPath = currentLookupBasePath;
            DialogResult result = browsePath.ShowDialog();
            if (result == DialogResult.Cancel)
                return;

            currentChannelLogoPath = browsePath.SelectedPath + @"\";
            tbChannelLogoPath.Text = currentChannelLogoPath;
        }
    }
}
