﻿#
# This script is intended to create a Build Server instance as an Azure VM.
# Before running the script you need to modify the first section to provide 
# the correct network configuration for the new machine. Then run it in the 
# PowerShell console e.g. Windows PowerShell ISE. 
#
# For details see: https://azure.microsoft.com/en-us/documentation/articles/virtual-machines-windows-capture-image/
#



$rgName = "BuildServers"
$storageAccName = "buildservers6531"
$vmName = "BuildServer-01"
$vmSize = "Standard_DS1"
$computerName = $vmName
$osDiskName = "System"
$location="West US"

$pip = Get-AzureRmPublicIpAddress -Name "BuildAgent-01" -ResourceGroupName $rgName
$vm = Get-AzureRmVirtualNetwork -Name BuildServers -ResourceGroupName BuildServers

$subnetconfig = Get-AzureRmVirtualNetworkSubnetConfig -VirtualNetwork $vm
$vnet = Get-AzureRmVirtualNetwork -Name BuildServers -ResourceGroupName BuildServers
$nic = Get-AzureRmNetworkInterface -Name "buildagent-01175" -ResourceGroupName $rgName


#Enter a new admin user name and password in the pop-up for the following
$cred = Get-Credential

#Get the storage account where the captured image is stored
$storageAcc = Get-AzureRmStorageAccount -ResourceGroupName $rgName -AccountName $storageAccName

#Set the VM name and size
$vmConfig = New-AzureRmVMConfig -VMName $vmName -VMSize $vmSize

#Set the Windows operating system configuration and add the NIC
$vm = Set-AzureRmVMOperatingSystem -VM $vmConfig -Windows -ComputerName $computerName -Credential $cred -ProvisionVMAgent -EnableAutoUpdate

$vm = Add-AzureRmVMNetworkInterface -VM $vm -Id $nic.Id

#Create the OS disk URI
$osDiskUri = '{0}vhds/{1}{2}.vhd' -f $storageAcc.PrimaryEndpoints.Blob.ToString(), $vmName.ToLower(), $osDiskName
$urlOfCapturedImageVhd = '{0}system/Microsoft.Compute/Images/vhds/templ-osDisk.2397cc9d-e7d8-4b40-b8b0-863cf130d119.vhd' -f $storageAcc.PrimaryEndpoints.Blob.ToString()

#Configure the OS disk to be created from image (-CreateOption fromImage) and give the URL of the captured image VHD for the -SourceImageUri parameter.
#We found this URL in the local JSON template in the previous sections.
$vm = Set-AzureRmVMOSDisk -VM $vm -Name $osDiskName -VhdUri $osDiskUri -CreateOption fromImage -SourceImageUri $urlOfCapturedImageVhd -Windows

#Create the new VM
New-AzureRmVM -ResourceGroupName $rgName -Location $location -VM $vm