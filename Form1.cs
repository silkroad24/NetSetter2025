using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Windows.Forms;
using Newtonsoft.Json;
using System.Diagnostics;

namespace NetSetter
{
    public partial class Form1 : Form
    {
        private Dictionary<string, List<NetworkProfile>> allProfiles = new Dictionary<string, List<NetworkProfile>>();
        private string ProfilesDir = "Profiles";

        public Form1()
        {
            InitializeComponent();
            if (!Directory.Exists(ProfilesDir))
                Directory.CreateDirectory(ProfilesDir);

            LoadAdapters();
            LoadAllProfiles();
            if (cbAdapterList.Items.Count > 0)
            {
                cbAdapterList.SelectedIndex = 0;
            }
        }

        private void LoadAdapters()
        {
            cbAdapterList.Items.Clear();
            var adapters = NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.NetworkInterfaceType != NetworkInterfaceType.Loopback);
            foreach (var adapter in adapters)
            {
                cbAdapterList.Items.Add(adapter.Name);
            }
        }

        private void LoadAllProfiles()
        {
            allProfiles.Clear();
            var files = Directory.GetFiles(ProfilesDir, "*.json");
            foreach (var file in files)
            {
                string adapterName = Path.GetFileNameWithoutExtension(file);
                try
                {
                    var json = File.ReadAllText(file);
                    var ps = JsonConvert.DeserializeObject<List<NetworkProfile>>(json);
                    if (ps != null)
                        allProfiles[adapterName] = ps;
                }
                catch { }
            }
        }

        private string SelectedAdapter => cbAdapterList.SelectedItem?.ToString();
        private List<NetworkProfile> CurrentProfileList
        {
            get
            {
                if (SelectedAdapter == null)
                    return null;
                if (!allProfiles.ContainsKey(SelectedAdapter))
                    allProfiles[SelectedAdapter] = new List<NetworkProfile>();
                return allProfiles[SelectedAdapter];
            }
        }
        private string AdapterProfilePath
            => SelectedAdapter == null ? null : Path.Combine(ProfilesDir, $"{SelectedAdapter}.json");

        private void cbAdapterList_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadProfilesOfCurrentAdapter();
        }

        private void LoadProfilesOfCurrentAdapter()
        {
            lbProfiles.Items.Clear();
            var profiles = CurrentProfileList;
            if (profiles != null)
            {
                foreach (var profile in profiles)
                {
                    lbProfiles.Items.Add(profile.ProfileName);
                }
            }
            // 默认选中第一个配置
            if (lbProfiles.Items.Count > 0)
                lbProfiles.SelectedIndex = 0;
            else
                ClearProfileFields();
        }

        private void SaveProfilesOfCurrentAdapter()
        {
            if (SelectedAdapter == null) return;
            File.WriteAllText(AdapterProfilePath, JsonConvert.SerializeObject(CurrentProfileList, Formatting.Indented));
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            if (SelectedAdapter == null)
            {
                MessageBox.Show("请先选择一个网卡");
                return;
            }
            var profiles = CurrentProfileList;
            var p = new NetworkProfile
            {
                ProfileName = txtProfileName.Text,
                IpAddress = txtIP.Text,
                SubnetMask = txtSubnet.Text,
                Gateway = txtGateway.Text,
                Dns1 = txtDns1.Text,
                Dns2 = txtDns2.Text
            };
            profiles.Add(p);
            SaveProfilesOfCurrentAdapter();
            LoadAllProfiles();
            LoadProfilesOfCurrentAdapter();
            lbProfiles.SelectedIndex = lbProfiles.Items.Count - 1;
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            if (SelectedAdapter == null) return;
            int idx = lbProfiles.SelectedIndex;
            if (idx >= 0)
            {
                var profiles = CurrentProfileList;
                var p = profiles[idx];
                p.ProfileName = txtProfileName.Text;
                p.IpAddress = txtIP.Text;
                p.SubnetMask = txtSubnet.Text;
                p.Gateway = txtGateway.Text;
                p.Dns1 = txtDns1.Text;
                p.Dns2 = txtDns2.Text;
                SaveProfilesOfCurrentAdapter();
                LoadAllProfiles();
                LoadProfilesOfCurrentAdapter();
                lbProfiles.SelectedIndex = idx;
            }
        }

        private void lbProfiles_SelectedIndexChanged(object sender, EventArgs e)
        {
            int idx = lbProfiles.SelectedIndex;
            if (idx >= 0)
            {
                var profiles = CurrentProfileList;
                var p = profiles[idx];
                txtProfileName.Text = p.ProfileName;
                txtIP.Text = p.IpAddress;
                txtSubnet.Text = p.SubnetMask;
                txtGateway.Text = p.Gateway;
                txtDns1.Text = p.Dns1;
                txtDns2.Text = p.Dns2;
            }
            else
            {
                ClearProfileFields();
            }
        }

        private void ClearProfileFields()
        {
            txtProfileName.Text = "";
            txtIP.Text = "";
            txtSubnet.Text = "";
            txtGateway.Text = "";
            txtDns1.Text = "";
            txtDns2.Text = "";
        }

        private void btnApply_Click(object sender, EventArgs e)
        {
            if (SelectedAdapter == null)
            {
                MessageBox.Show("请选择一个网卡");
                return;
            }
            int idx = lbProfiles.SelectedIndex;
            if (idx < 0)
            {
                MessageBox.Show("请选择一个配置");
                return;
            }
            var p = CurrentProfileList[idx];
            ApplyNetworkProfile(SelectedAdapter, p);
        }

        private void btnDHCP_Click(object sender, EventArgs e)
        {
            if (SelectedAdapter == null)
            {
                MessageBox.Show("请选择一个网卡");
                return;
            }
            try
            {
                // 恢复DHCP获取IP、DNS
                RunNetsh($"interface ip set address name=\"{SelectedAdapter}\" source=dhcp");
                RunNetsh($"interface ip set dns name=\"{SelectedAdapter}\" source=dhcp");
                MessageBox.Show("已切换到DHCP，请检查网络");
            }
            catch (Exception ex)
            {
                MessageBox.Show("切换DHCP失败: " + ex.Message);
            }
        }

        public void ApplyNetworkProfile(string adapterName, NetworkProfile profile)
        {
            try
            {
                // 1. IP and subnet
                RunNetsh($"interface ip set address name=\"{adapterName}\" static {profile.IpAddress} {profile.SubnetMask} {profile.Gateway} 1");
                // 2. DNS
                RunNetsh($"interface ip set dns name=\"{adapterName}\" static {profile.Dns1}");
                if (!string.IsNullOrEmpty(profile.Dns2))
                {
                    RunNetsh($"interface ip add dns name=\"{adapterName}\" {profile.Dns2} index=2");
                }
                MessageBox.Show("配置已应用，请检查网络");
            }
            catch (Exception ex)
            {
                MessageBox.Show("应用配置失败: " + ex.Message);
            }
        }

        private void RunNetsh(string arguments)
        {
            ProcessStartInfo psi = new ProcessStartInfo("netsh", arguments);
            psi.Verb = "runas";      // 管理员权限
            psi.CreateNoWindow = true;
            psi.UseShellExecute = true;
            Process.Start(psi).WaitForExit();
        }




        private void btnDelete_Click(object sender, EventArgs e)
        {
            int idx = lbProfiles.SelectedIndex;
            if (idx < 0)
            {
                MessageBox.Show("请先选择要删除的配置");
                return;
            }
            var profiles = CurrentProfileList;
            var confirm = MessageBox.Show($"确认删除配置“{profiles[idx].ProfileName}”吗？", "确认删除", MessageBoxButtons.YesNo);
            if (confirm == DialogResult.Yes)
            {
                profiles.RemoveAt(idx);
                SaveProfilesOfCurrentAdapter();
                LoadAllProfiles();
                LoadProfilesOfCurrentAdapter();
                if (lbProfiles.Items.Count > 0)
                    lbProfiles.SelectedIndex = Math.Min(idx, lbProfiles.Items.Count - 1);
                else
                    ClearProfileFields();
            }
        }
        // 导出配置到外部文件
        private void btnExport_Click(object sender, EventArgs e)
        {
            int idx = lbProfiles.SelectedIndex;
            if (idx < 0)
            {
                MessageBox.Show("请先选择要导出的配置");
                return;
            }
            var profile = CurrentProfileList[idx];
            using (SaveFileDialog dlg = new SaveFileDialog())
            {
                dlg.Filter = "JSON文件|*.json";
                dlg.FileName = $"{profile.ProfileName}.json";
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        string json = JsonConvert.SerializeObject(profile, Formatting.Indented);
                        File.WriteAllText(dlg.FileName, json);
                        MessageBox.Show("导出成功！");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("导出失败: " + ex.Message);
                    }
                }
            }
        }
        // 导入配置文件，追加到当前网卡配置列表
        private void btnImport_Click(object sender, EventArgs e)
        {
            if (SelectedAdapter == null)
            {
                MessageBox.Show("请先选择一个网卡");
                return;
            }
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Filter = "JSON文件|*.json";
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        string content = File.ReadAllText(dlg.FileName);
                        // 支持导入单个 NetworkProfile 或 NetworkProfile 列表
                        List<NetworkProfile> importedProfiles = null;
                        try
                        {
                            importedProfiles = JsonConvert.DeserializeObject<List<NetworkProfile>>(content);
                        }
                        catch
                        {
                            var singleProfile = JsonConvert.DeserializeObject<NetworkProfile>(content);
                            if (singleProfile != null)
                                importedProfiles = new List<NetworkProfile> { singleProfile };
                        }
                        if (importedProfiles == null || importedProfiles.Count == 0)
                        {
                            MessageBox.Show("导入文件格式错误或内容为空");
                            return;
                        }
                        var profiles = CurrentProfileList;
                        foreach (var p in importedProfiles)
                        {
                            // 避免重名，自动后缀
                            string originalName = p.ProfileName;
                            int suffix = 1;
                            while (profiles.Any(pr => pr.ProfileName == p.ProfileName))
                            {
                                p.ProfileName = originalName + "_" + suffix++;
                            }
                            profiles.Add(p);
                        }
                        SaveProfilesOfCurrentAdapter();
                        LoadAllProfiles();
                        LoadProfilesOfCurrentAdapter();
                        lbProfiles.SelectedIndex = lbProfiles.Items.Count - 1; // 选中最新导入的
                        MessageBox.Show("导入成功");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("导入失败: " + ex.Message);
                    }
                }
            }
        }



    }
}
