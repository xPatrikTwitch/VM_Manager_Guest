using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Windows;

namespace VM_Manager_Guest
{
    public partial class MainWindow : Window
    {
        Process monitoring_smi_process = new Process();
        HttpListener api_listener;

        string version_string = "1.0.0";

        // Default config values are set here, when a config file is found they get overridden
        int api_port = 6050;
        int monitoring_refresh_rate_ms = 500;
        bool kill_smi_on_start = true;

        string json_data_string = "";

        public MainWindow()
        {
            InitializeComponent();

            if (File.Exists(Environment.CurrentDirectory + "/config.json"))
            {
                read_config();
            }
            else
            {
                generate_config();
            }

            if (kill_smi_on_start == true)
            {
                Process smi_kill = new Process();
                smi_kill.StartInfo.UseShellExecute = false;
                smi_kill.StartInfo.FileName = "cmd.exe";
                smi_kill.StartInfo.Arguments = "taskkill /f /im nvidia-smi.exe";
                smi_kill.StartInfo.CreateNoWindow = true;
                smi_kill.Start();
            }

            api_start();
            start_smi(0); //gpu_id is 0 for the first nvidia gpu in the VM
            text_status.Text = "Version: " + version_string + " | Hosting on port " + api_port.ToString();
        }

        private void read_config()
        {
            try
            {
                string config_json_string = "";
                using (StreamReader sr = new StreamReader(Environment.CurrentDirectory + "/config.json"))
                {
                    config_json_string = sr.ReadToEnd();
                }
                JObject config_json = JObject.Parse(config_json_string);
                api_port = (int)config_json.SelectToken("api_port");
                monitoring_refresh_rate_ms = (int)config_json.SelectToken("monitoring_refresh_rate_ms");
                kill_smi_on_start = (bool)config_json.SelectToken("kill_smi_on_start"); ;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to read config (Delete config file to reset to default settings) " + ex.ToString());
                this.Close();
            }
        }

        private void generate_config()
        {
            json_config default_config = new json_config()
            {
                api_port = api_port,
                monitoring_refresh_rate_ms = monitoring_refresh_rate_ms,
                kill_smi_on_start = kill_smi_on_start,
            };
            string config_json_string = JsonConvert.SerializeObject(default_config, Formatting.Indented);
            using (StreamWriter sw = new StreamWriter(Environment.CurrentDirectory + "/config.json"))
            {
                sw.Write(config_json_string);
            }
        }

        private void start_smi(int gpu_id)
        {
            monitoring_smi_process.StartInfo.UseShellExecute = false;
            monitoring_smi_process.StartInfo.FileName = "C:/Windows/System32/nvidia-smi.exe";
            monitoring_smi_process.StartInfo.Arguments = "-i " + gpu_id.ToString() + " " + "--query-gpu=name,power.draw,power.limit,memory.used,memory.total,temperature.gpu,fan.speed" + " --format=csv,noheader -lms " + monitoring_refresh_rate_ms.ToString();
            monitoring_smi_process.StartInfo.CreateNoWindow = true;
            monitoring_smi_process.StartInfo.RedirectStandardOutput = true;
            monitoring_smi_process.StartInfo.RedirectStandardError = true;

            monitoring_smi_process.OutputDataReceived += new DataReceivedEventHandler((s, e) =>
            {
                string[] output_split = e.Data.Split(',');

                string gpu_name = output_split[0].TrimStart().TrimEnd();
                string gpu_power_draw = output_split[1].TrimStart().TrimEnd().Split(' ')[0].Split('.')[0];
                string gpu_power_limit = output_split[2].TrimStart().TrimEnd().Split(' ')[0].Split('.')[0];
                string gpu_memory_usage = output_split[3].TrimStart().TrimEnd().Split(' ')[0];
                string gpu_memory_total = output_split[4].TrimStart().TrimEnd().Split(' ')[0];
                string gpu_temperature = output_split[5].TrimStart().TrimEnd().Split(' ')[0];
                string gpu_fan = output_split[6].TrimStart().TrimEnd().Split(' ')[0];

                if (gpu_name == "[N/A]") { gpu_name = ""; }
                if (gpu_power_draw == "[N/A]") { gpu_power_draw = "0"; }
                if (gpu_power_draw == "[N/A]") { gpu_power_draw = "0"; }
                if (gpu_power_limit == "[N/A]") { gpu_power_limit = "0"; }
                if (gpu_memory_usage == "[N/A]") { gpu_memory_usage = "0"; }
                if (gpu_memory_total == "[N/A]") { gpu_memory_total = "0"; }
                if (gpu_temperature == "[N/A]") { gpu_temperature = "0"; }
                if (gpu_fan == "[N/A]") { gpu_fan = "0"; }

                Dictionary<string, string> points = new Dictionary<string, string>
                {
                    { "gpu_name", gpu_name },
                    { "gpu_power_draw", gpu_power_draw },
                    { "gpu_power_limit", gpu_power_limit },
                    { "gpu_memory_usage", gpu_memory_usage },
                    { "gpu_memory_total", gpu_memory_total },
                    { "gpu_temperature", gpu_temperature },
                    { "gpu_fan", gpu_fan }
                };

                json_data_string = JsonConvert.SerializeObject(points, Formatting.None);

            });

            monitoring_smi_process.Start();
            monitoring_smi_process.BeginOutputReadLine();
            monitoring_smi_process.BeginErrorReadLine();
        }

        public void api_start()
        {
            api_listener = new HttpListener();
            api_listener.Prefixes.Add("http://*:" + api_port.ToString() + "/");
            api_listener.Start();
            api_receive();
        }

        private void api_receive()
        {
            api_listener.BeginGetContext(new AsyncCallback(ListenerCallback), api_listener);
        }

        private void ListenerCallback(IAsyncResult result)
        {
            if (api_listener.IsListening)
            {
                var context = api_listener.EndGetContext(result);
                var response = context.Response;
                response.StatusCode = (int)HttpStatusCode.OK;
                response.ContentType = "text/plain";
                response.OutputStream.Write(Encoding.ASCII.GetBytes(json_data_string));
                response.OutputStream.Close();
                api_receive();
            }
        }



        public class json_config
        {
            public int api_port { get; set; }
            public int monitoring_refresh_rate_ms { get; set; }
            public bool kill_smi_on_start { get; set; }
        }
    }
}