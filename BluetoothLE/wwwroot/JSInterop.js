﻿window.ble = {};

// BluetoothUUID

window.ble.bluetoothUUIDGetService = (name) => {
    let result = BluetoothUUID.getService(name);
    return result;
}

window.ble.bluetoothUUIDGetCharacteristic = (name) => {
    let result = BluetoothUUID.getCharacteristic(name);
    return result;
}

window.ble.bluetoothUUIDGetDescriptor = (name) => {
    let result = BluetoothUUID.getDescriptor(name);
    return result;
}

window.ble.bluetoothUUIDCanonicalUUID = (alias) => {
    let result = BluetoothUUID.canonicalUUID(alias);
    return result;
}

// End BluetoothUUID

// Helpers

var PairedBluetoothDevices = [];

function getPairedBluetoothDeviceById(deviceId) {
    var device = PairedBluetoothDevices.filter(function (item) {
        return item.id == deviceId;
    });

    return device[0];
}

async function getCharacteristic(deviceId, serviceId, characteristicId) {
    var device = getPairedBluetoothDeviceById(deviceId);

    var service = await device.gatt.getPrimaryService(serviceId);
    var characteristic = await service.getCharacteristic(characteristicId);
    return characteristic;
}

// End Helpers


// Device

function convertBluetoothAdvertisingEventToInternal(event) {
    return {
        "InternalAppearance": event.appearance,
        "InternalDevice": event.device,
        "InternalManufacturerData": event.manufacturerData,
        "InternalName": event.name,
        "InternalRssi": event.rssi,
        "InternalServiceData": event.serviceData,
        "InternalTxPower": event.txPower,
        "InternalUuids": event.uuids,
    }
}

var advertisementsHandlers = [];

window.ble.setAdvertisementReceivedHandler = (handler, deviceId) => {

    var addedHandler = advertisementsHandlers.find(x => x.deviceId == deviceId);
    if (addedHandler != null) {

        // Remove previous handler for specific device.
        advertisementsHandlers =
            advertisementsHandlers.filter(item => item.deviceId != addedHandler.deviceId);
    }

    if (handler != null) {

        // Add new handler for specific device.
        advertisementsHandlers.push({
            handler: handler,
            deviceId: deviceId
        });
    }
}

window.ble.watchAdvertisements = async (deviceId) => {
    var device = getPairedBluetoothDeviceById(deviceId);
    if (!device.watchingAdvertisements) {
        device.addEventListener('advertisementreceived', handleAdvertisementReceived);
        device.watchAdvertisements();
    }
}

async function handleAdvertisementReceived(event) {

    // get handler for specific device.
    var handler = advertisementsHandlers.find(x => x.deviceId == event.device.id);
    if (handler != null) {
        var convertedEvent = convertBluetoothAdvertisingEventToInternal(event);
        await handler.handler.invokeMethodAsync('HandleAdvertisementReceived', convertedEvent);
    }
}

window.ble.forget = async (deviceId) => {
    var device = getPairedBluetoothDeviceById(deviceId);
    device.forget();
}

// End Device


// Service

function convertPrimaryServiceToInternal(service, deviceId) {
    return {
        "InternalIsPrimary": service.isPrimary,
        "InternalUuid": service.uuid,
        "InternalDeviceUuid": deviceId,
    }
}

window.ble.getPrimaryService = async (serviceId, deviceId) => {
    var device = getPairedBluetoothDeviceById(deviceId);
    var primaryService = await device.gatt.getPrimaryService(serviceId);
    return convertPrimaryServiceToInternal(primaryService, deviceId)
}

window.ble.getPrimaryServices = async (serviceId, deviceId) => {
    var device = getPairedBluetoothDeviceById(deviceId);
    var primaryServices = await device.gatt.getPrimaryServices(serviceId);
    return primaryServices.map(x => convertPrimaryServiceToInternal(x, deviceId));
}

// End Service


// Characteristic

function convertCharacteristicToInternal(characteristic, deviceId, serviceId) {
    return {
        "InternalProperties": {
            "InternalAuthenticatedSignedWrites": characteristic.properties.authenticatedSignedWrites,
            "InternalBroadcast": characteristic.properties.broadcast,
            "InternalIndicate": characteristic.properties.indicate,
            "InternalNotify": characteristic.properties.notify,
            "InternalRead": characteristic.properties.read,
            "InternalReliableWrite": characteristic.properties.reliableWrite,
            "InternalWritableAuxiliaries": characteristic.properties.writableAuxiliaries,
            "InternalWrite": characteristic.properties.write,
            "InternalWriteWithoutResponse": characteristic.properties.writeWithoutResponse,
        },
        "InternalUuid": characteristic.uuid,
        "InternalValue": characteristic.value,
        "InternalDeviceUuid": deviceId,
        "InternalServiceUuid": serviceId,
    }
}

window.ble.getCharacteristic = async (serviceId, characteristicId, deviceId) => {

    var characteristic = await getCharacteristic(deviceId, serviceId, characteristicId);
    return convertCharacteristicToInternal(characteristic, deviceId, serviceId)
}

window.ble.getCharacteristics = async (serviceId, characteristicId, deviceId) => {
    var device = getPairedBluetoothDeviceById(deviceId);
    var service = await device.gatt.getPrimaryService(serviceId);
    var characteristics = await service.getCharacteristics(characteristicId);
    return characteristics.map(x => convertCharacteristicToInternal(x, deviceId, serviceId));
}

window.ble.getCharacteristicsWithoutUUID = async (serviceId, deviceId) => {
    var device = getPairedBluetoothDeviceById(deviceId);
    var service = await device.gatt.getPrimaryService(serviceId);
    var characteristics = await service.getCharacteristics();
    return characteristics.map(x => convertCharacteristicToInternal(x, deviceId, serviceId));
}

// End Characteristic


// Descriptors

function convertDescriptorToInternal(descriptor, characteristicId, deviceId, serviceId) {
    return {
        "InternalUuid": descriptor.uuid,
        "InternalValue": descriptor.value,
        "InternalDeviceUuid": deviceId,
        "InternalCharacteristicUuid": characteristicId,
        "InternalServiceUuid": serviceId,
    }
}

window.ble.getDescriptor = async (descriptorId, serviceId, characteristicId, deviceId) => {

    var characteristic = await getCharacteristic(deviceId, serviceId, characteristicId);
    var descriptor = await characteristic.getDescriptor(descriptorId);
    return convertDescriptorToInternal(descriptor, characteristicId, deviceId, serviceId);
}

window.ble.getDescriptors = async (descriptorId, serviceId, characteristicId, deviceId) => {

    var characteristic = await getCharacteristic(deviceId, serviceId, characteristicId);
    var descriptors = await characteristic.getDescriptors(descriptorId);
    return descriptors.map(x => convertDescriptorToInternal(x, characteristicId, deviceId, serviceId));
}

// End Descriptors


// Characteristic Start/Stop notifications

window.ble.startNotification = async (deviceId, serviceId, characteristicId) => {

    var characteristic = await getCharacteristic(deviceId, serviceId, characteristicId);
    await characteristic.startNotifications();
    characteristic.addEventListener('characteristicvaluechanged', handleCharacteristicValueChanged);
}

window.ble.stopNotification = async (deviceId, serviceId, characteristicId) => {

    var characteristic = await getCharacteristic(deviceId, serviceId, characteristicId);
    await characteristic.stopNotifications();
    characteristic.removeEventListener('characteristicvaluechanged', handleCharacteristicValueChanged);
}

// End Characteristic Start/Stop notifications


// Characteristic value changed

var characteristicValueHandlers = [];

window.ble.setCharacteristicValueChangedHandler = (handler, deviceId, serviceId, characteristicId) => {

    var addedHandler = characteristicValueHandlers.find(
        x => x.deviceId == deviceId
            && x.serviceId == serviceId
            && x.characteristicId == characteristicId);

    if (addedHandler != null) {

        // Remove previous handler for specific device.
        characteristicValueHandlers =
            characteristicValueHandlers.filter(
                x => x.deviceId != addedHandler.deviceId
                    || x.serviceId != addedHandler.serviceId
                    || x.characteristicId != addedHandler.characteristicId);
    }

    if (handler != null) {

        // Add new handler for specific device.
        characteristicValueHandlers.push({
            handler: handler,
            deviceId: deviceId,
            serviceId: serviceId,
            characteristicId: characteristicId
        });
    }
}

async function handleCharacteristicValueChanged(event) {

    var target = event.target;
    // get handler for specific device.
    var handler = characteristicValueHandlers.find(
        x => x.deviceId == target.service.device.id
            && x.serviceId == target.service.uuid
            && x.characteristicId == target.uuid);
    if (handler != null) {

        var value = target.value;

        var uint8Array = new Uint8Array(value.buffer);

        var array = Array.from(uint8Array)
        await handler.handler.invokeMethodAsync('HandleCharacteristicValueChanged', event.target.service.uuid, event.target.uuid, array);
    }
}

// End Characteristic value changed


// Characteristic read/write value

window.ble.characteristicWriteValue = async (deviceId, serviceId, characteristicId, value) => {

    var characteristic = await getCharacteristic(deviceId, serviceId, characteristicId);
    var b = Uint8Array.from(value);
    await characteristic.writeValue(b);
}

window.ble.characteristicWriteValueWithoutResponse = async (deviceId, serviceId, characteristicId, value) => {

    var characteristic = await getCharacteristic(deviceId, serviceId, characteristicId);
    var b = Uint8Array.from(value);
    await characteristic.writeValueWithoutResponse(b);
}

window.ble.characteristicWriteValueWithResponse = async (deviceId, serviceId, characteristicId, value) => {

    var characteristic = await getCharacteristic(deviceId, serviceId, characteristicId);
    var b = Uint8Array.from(value);
    await characteristic.writeValueWithResponse(b);
}

window.ble.characteristicReadValue = async (deviceId, serviceId, characteristicId) => {

    var characteristic = await getCharacteristic(deviceId, serviceId, characteristicId);

    var value = await characteristic.readValue();
    var uint8Array = new Uint8Array(value.buffer);
    var array = Array.from(uint8Array);
    return array;
}

// End Characteristic write value


// Descriptor read/write

window.ble.descriptorReadValue = async (deviceId, serviceId, characteristicId, descriptorId) => {

    var characteristic = await getCharacteristic(deviceId, serviceId, characteristicId);
    var descriptor = await characteristic.getDescriptor(descriptorId);

    var value = await descriptor.readValue();
    var uint8Array = new Uint8Array(value.buffer);
    var array = Array.from(uint8Array);
    return array;
}

window.ble.descriptorWriteValue = async (deviceId, serviceId, characteristicId, descriptorId, value) => {

    var characteristic = await getCharacteristic(deviceId, serviceId, characteristicId);
    var descriptor = await characteristic.getDescriptor(descriptorId);

    var b = Uint8Array.from(value);
    await descriptor.writeValue(b);
}

// End Descriptor read/write


// Bluetooth

function convertDeviceToInternal(device) {
    return {
        "InternalName": device.name,
        "InternalId": device.id,
        "InternalGatt": {
            "InternalDeviceUuid": device.id,
            "InternalConnected": device.gatt.connected
        }
    };
}

window.ble.getDeviceById = (deviceId) => {
    return convertDeviceToInternal(getPairedBluetoothDeviceById(deviceId));
}

window.ble.connectDevice = async (deviceId) => {
    var device = getPairedBluetoothDeviceById(deviceId);

    if (device !== null) {
        await device.gatt.connect();
        return convertDeviceToInternal(device);
    }
    else {
        return null;
    }
}

window.ble.disconnectDevice = async (deviceId) => {
    var device = getPairedBluetoothDeviceById(deviceId);

    if (device !== null) {
        await device.gatt.disconnect();
        return convertDeviceToInternal(device);
    }
    else {
        return null;
    }
}

window.ble.referringDevice = () => {

    var device = navigator.bluetooth.referringDevice;
    if (device === undefined) {
        throw 'Referring device is not supporting';
    }
    else {
        return convertDeviceToInternal(device);
    }
}

window.ble.requestDevice = async (options) => {
    var objOptions = JSON.parse(options);
    var device = await navigator.bluetooth.requestDevice(objOptions);

    var alreadyPariedDevice = getPairedBluetoothDeviceById(device.id);
    if (alreadyPariedDevice != null) {
        var indexToRemove = PairedBluetoothDevices.findIndex(x => x.id == device.id);
        PairedBluetoothDevices.splice(indexToRemove, 1);
    }

    PairedBluetoothDevices.push(device);

    if (device !== null) {
        console.log('> Bluetooth Device selected.');
    }

    return window.ble.getDeviceById(device.id);
}

window.ble.getAvailability = async () => {
    return await navigator.bluetooth.getAvailability();
}

window.ble.getDevices = async () => {
    if (navigator.bluetooth.getDevices == undefined) {
        throw 'Get devices is not supporting';
    } else {
        var devices = await navigator.bluetooth.getDevices();

        devices.forEach((device) => {
            var alreadyPariedDevice = getPairedBluetoothDeviceById(device.id);
            if (alreadyPariedDevice != null) {
                var indexToRemove = PairedBluetoothDevices.findIndex(x => x.id == device.id);
                PairedBluetoothDevices.splice(indexToRemove, 1);
            }

            PairedBluetoothDevices.push(device);
        });

        return devices.map(x => convertDeviceToInternal(x));
    }
}

// End Bluetooth


// On disconnected from device

var DeviceDisconnectionHandler = [];

async function onDisconnected() {

    console.log('> Bluetooth Device disconnected.');

    await DeviceDisconnectionHandler.invokeMethodAsync('HandleDeviceDisconnected');
}

window.ble.addDeviceDisconnectionHandler = (deviceDisconnectHandler, deviceUuid) => {
    var device = getPairedBluetoothDeviceById(deviceUuid);

    if (deviceDisconnectHandler !== null) {

        DeviceDisconnectionHandler = deviceDisconnectHandler;
        device.addEventListener('gattserverdisconnected', onDisconnected);
    }
    else if (DeviceDisconnectionHandler !== null && DeviceDisconnectionHandler.length > 0) {

        device.removeEventListener('gattserverdisconnected', onDisconnected);
        DeviceDisconnectionHandler = null;
    }
}

// End On disconnected from device


// On availability changed from bluetooth

var BluetoothAvailabilityHandler = [];

async function onAvailabilityChanged() {

    await BluetoothAvailabilityHandler.invokeMethodAsync('HandleAvailabilityChanged');
}

window.ble.addBluetoothAvailabilityHandler = (bluetoothAvailabilityHandler) => {

    if (bluetoothAvailabilityHandler !== null) {

        BluetoothAvailabilityHandler = bluetoothAvailabilityHandler;
        navigator.bluetooth.addEventListener('onavailabilitychanged', onDisconnected);
    }
    else if (BluetoothAvailabilityHandler !== null && BluetoothAvailabilityHandler.length > 0) {

        navigator.bluetooth.removeEventListener('onavailabilitychanged', onDisconnected);
        BluetoothAvailabilityHandler = null;
    }
}

// End On availability changed from bluetooth