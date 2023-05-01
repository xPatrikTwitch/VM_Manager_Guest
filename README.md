__This app is used for monitoring a GPU thats used in a windows virtual machine (on proxmox)__

*This is only needed when using gpu pcie passthrough! For vGPU only the host app is needed
(Currently only nvidia gpu are supported)

Having issues with the api?
---
If you are having issues accessing the api then adding this firewall setting should fix it:
``netsh advfirewall firewall add rule name="VM_Manager_Guest" dir=in action=allow protocol=TCP localport=5399``

(Run in cmd as administrator) Replace "6050" with your port if custom port is configured
*If you are still getting no response make sure that the app is running as administrator

Configuration file is automatically created on first startup
---
This is the default config:
```
{
  "api_port": 6050,
  "monitoring_refresh_rate_ms": 500,
  "kill_smi_on_start": true
}
```
"api_port" is the port on what the api is hosted
"monitoring_refresh_rate_ms" is how fast does the monitoring data refresh, 500ms is a good value and faster is not needed
"kill_smi_on_start" if enabled will taskkill nvidia-smi.exe on start (can be left running when this app crashed or when multiple instances of this app are running)

This app should be run at startup. This can be easily done by adding a new task in the task scheduler on windows

*This is not the best code but works for me...
