import { MemoryPackWriter } from "./MemoryPackWriter";
import { MemoryPackReader } from "./MemoryPackReader";
import { KeyBindingData } from "./KeyBindingData";

export class SettingsSnapshot {
    selectedRenderer: number;
    selectedResolution: number;
    selectedDisplayMode: number;
    vsync: boolean;
    fpsLimit: number;
    antialias: number;
    shadowCascadeLevel: number;
    shadowResolution: number;
    renderDistance: number;
    lowLatency: boolean;
    lineWidth: number;
    masterVolume: number;
    musicVolume: number;
    effectsVolume: number;
    muteAll: boolean;
    remasteredMusic: boolean;
    fov: number;
    followY: number;
    followZ: number;
    smoothFov: boolean;
    keyBindings: (KeyBindingData | null)[] | null;

    constructor() {
        this.selectedRenderer = 0;
        this.selectedResolution = 0;
        this.selectedDisplayMode = 0;
        this.vsync = false;
        this.fpsLimit = 0;
        this.antialias = 0;
        this.shadowCascadeLevel = 0;
        this.shadowResolution = 0;
        this.renderDistance = 0;
        this.lowLatency = false;
        this.lineWidth = 0;
        this.masterVolume = 0;
        this.musicVolume = 0;
        this.effectsVolume = 0;
        this.muteAll = false;
        this.remasteredMusic = false;
        this.fov = 0;
        this.followY = 0;
        this.followZ = 0;
        this.smoothFov = false;
        this.keyBindings = null;

    }

    static serialize(value: SettingsSnapshot | null): Uint8Array {
        const writer = MemoryPackWriter.getSharedInstance();
        this.serializeCore(writer, value);
        return writer.toArray();
    }

    static serializeCore(writer: MemoryPackWriter, value: SettingsSnapshot | null): void {
        if (value == null) {
            writer.writeNullObjectHeader();
            return;
        }

        writer.writeObjectHeader(21);
        writer.writeInt32(value.selectedRenderer);
        writer.writeInt32(value.selectedResolution);
        writer.writeInt32(value.selectedDisplayMode);
        writer.writeBoolean(value.vsync);
        writer.writeInt32(value.fpsLimit);
        writer.writeInt32(value.antialias);
        writer.writeInt32(value.shadowCascadeLevel);
        writer.writeInt32(value.shadowResolution);
        writer.writeInt32(value.renderDistance);
        writer.writeBoolean(value.lowLatency);
        writer.writeFloat32(value.lineWidth);
        writer.writeFloat32(value.masterVolume);
        writer.writeFloat32(value.musicVolume);
        writer.writeFloat32(value.effectsVolume);
        writer.writeBoolean(value.muteAll);
        writer.writeBoolean(value.remasteredMusic);
        writer.writeFloat32(value.fov);
        writer.writeInt32(value.followY);
        writer.writeInt32(value.followZ);
        writer.writeBoolean(value.smoothFov);
        writer.writeArray(value.keyBindings, (writer, x) => KeyBindingData.serializeCore(writer, x));

    }

    static serializeArray(value: (SettingsSnapshot | null)[] | null): Uint8Array {
        const writer = MemoryPackWriter.getSharedInstance();
        this.serializeArrayCore(writer, value);
        return writer.toArray();
    }

    static serializeArrayCore(writer: MemoryPackWriter, value: (SettingsSnapshot | null)[] | null): void {
        writer.writeArray(value, (writer, x) => SettingsSnapshot.serializeCore(writer, x));
    }

    static deserialize(buffer: ArrayBuffer): SettingsSnapshot | null {
        return this.deserializeCore(new MemoryPackReader(buffer));
    }

    static deserializeCore(reader: MemoryPackReader): SettingsSnapshot | null {
        const [ok, count] = reader.tryReadObjectHeader();
        if (!ok) {
            return null;
        }

        const value = new SettingsSnapshot();
        if (count == 21) {
            value.selectedRenderer = reader.readInt32()!;
            value.selectedResolution = reader.readInt32()!;
            value.selectedDisplayMode = reader.readInt32()!;
            value.vsync = reader.readBoolean()!;
            value.fpsLimit = reader.readInt32()!;
            value.antialias = reader.readInt32()!;
            value.shadowCascadeLevel = reader.readInt32()!;
            value.shadowResolution = reader.readInt32()!;
            value.renderDistance = reader.readInt32()!;
            value.lowLatency = reader.readBoolean()!;
            value.lineWidth = reader.readFloat32()!;
            value.masterVolume = reader.readFloat32()!;
            value.musicVolume = reader.readFloat32()!;
            value.effectsVolume = reader.readFloat32()!;
            value.muteAll = reader.readBoolean()!;
            value.remasteredMusic = reader.readBoolean()!;
            value.fov = reader.readFloat32()!;
            value.followY = reader.readInt32()!;
            value.followZ = reader.readInt32()!;
            value.smoothFov = reader.readBoolean()!;
            value.keyBindings = reader.readArray(reader => KeyBindingData.deserializeCore(reader));

        }
        else if (count > 21) {
            throw new Error("Current object's property count is larger than type schema, can't deserialize about versioning.");
        }
        else {
            if (count == 0) return value;
            value.selectedRenderer = reader.readInt32()!; if (count == 1) return value;
            value.selectedResolution = reader.readInt32()!; if (count == 2) return value;
            value.selectedDisplayMode = reader.readInt32()!; if (count == 3) return value;
            value.vsync = reader.readBoolean()!; if (count == 4) return value;
            value.fpsLimit = reader.readInt32()!; if (count == 5) return value;
            value.antialias = reader.readInt32()!; if (count == 6) return value;
            value.shadowCascadeLevel = reader.readInt32()!; if (count == 7) return value;
            value.shadowResolution = reader.readInt32()!; if (count == 8) return value;
            value.renderDistance = reader.readInt32()!; if (count == 9) return value;
            value.lowLatency = reader.readBoolean()!; if (count == 10) return value;
            value.lineWidth = reader.readFloat32()!; if (count == 11) return value;
            value.masterVolume = reader.readFloat32()!; if (count == 12) return value;
            value.musicVolume = reader.readFloat32()!; if (count == 13) return value;
            value.effectsVolume = reader.readFloat32()!; if (count == 14) return value;
            value.muteAll = reader.readBoolean()!; if (count == 15) return value;
            value.remasteredMusic = reader.readBoolean()!; if (count == 16) return value;
            value.fov = reader.readFloat32()!; if (count == 17) return value;
            value.followY = reader.readInt32()!; if (count == 18) return value;
            value.followZ = reader.readInt32()!; if (count == 19) return value;
            value.smoothFov = reader.readBoolean()!; if (count == 20) return value;
            value.keyBindings = reader.readArray(reader => KeyBindingData.deserializeCore(reader)); if (count == 21) return value;

        }
        return value;
    }

    static deserializeArray(buffer: ArrayBuffer): (SettingsSnapshot | null)[] | null {
        return this.deserializeArrayCore(new MemoryPackReader(buffer));
    }

    static deserializeArrayCore(reader: MemoryPackReader): (SettingsSnapshot | null)[] | null {
        return reader.readArray(reader => SettingsSnapshot.deserializeCore(reader));
    }
}
