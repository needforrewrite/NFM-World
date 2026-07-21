import { MemoryPackWriter } from "./MemoryPackWriter";
import { MemoryPackReader } from "./MemoryPackReader";

export class AvailableOptions {
    renderers: string[] | null;
    resolutions: string[] | null;
    displayModes: string[] | null;
    antialiasModes: string[] | null;
    shadowCascadeLevels: string[] | null;
    shadowResolutions: string[] | null;
    renderDistanceNames: string[] | null;

    constructor() {
        this.renderers = null;
        this.resolutions = null;
        this.displayModes = null;
        this.antialiasModes = null;
        this.shadowCascadeLevels = null;
        this.shadowResolutions = null;
        this.renderDistanceNames = null;

    }

    static serialize(value: AvailableOptions | null): Uint8Array {
        const writer = MemoryPackWriter.getSharedInstance();
        this.serializeCore(writer, value);
        return writer.toArray();
    }

    static serializeCore(writer: MemoryPackWriter, value: AvailableOptions | null): void {
        if (value == null) {
            writer.writeNullObjectHeader();
            return;
        }

        writer.writeObjectHeader(7);
        writer.writeArray(value.renderers, (writer, x) => writer.writeString(x));
        writer.writeArray(value.resolutions, (writer, x) => writer.writeString(x));
        writer.writeArray(value.displayModes, (writer, x) => writer.writeString(x));
        writer.writeArray(value.antialiasModes, (writer, x) => writer.writeString(x));
        writer.writeArray(value.shadowCascadeLevels, (writer, x) => writer.writeString(x));
        writer.writeArray(value.shadowResolutions, (writer, x) => writer.writeString(x));
        writer.writeArray(value.renderDistanceNames, (writer, x) => writer.writeString(x));

    }

    static serializeArray(value: (AvailableOptions | null)[] | null): Uint8Array {
        const writer = MemoryPackWriter.getSharedInstance();
        this.serializeArrayCore(writer, value);
        return writer.toArray();
    }

    static serializeArrayCore(writer: MemoryPackWriter, value: (AvailableOptions | null)[] | null): void {
        writer.writeArray(value, (writer, x) => AvailableOptions.serializeCore(writer, x));
    }

    static deserialize(buffer: ArrayBuffer): AvailableOptions | null {
        return this.deserializeCore(new MemoryPackReader(buffer));
    }

    static deserializeCore(reader: MemoryPackReader): AvailableOptions | null {
        const [ok, count] = reader.tryReadObjectHeader();
        if (!ok) {
            return null;
        }

        const value = new AvailableOptions();
        if (count == 7) {
            value.renderers = reader.readArray(reader => reader.readString()!);
            value.resolutions = reader.readArray(reader => reader.readString()!);
            value.displayModes = reader.readArray(reader => reader.readString()!);
            value.antialiasModes = reader.readArray(reader => reader.readString()!);
            value.shadowCascadeLevels = reader.readArray(reader => reader.readString()!);
            value.shadowResolutions = reader.readArray(reader => reader.readString()!);
            value.renderDistanceNames = reader.readArray(reader => reader.readString()!);

        }
        else if (count > 7) {
            throw new Error("Current object's property count is larger than type schema, can't deserialize about versioning.");
        }
        else {
            if (count == 0) return value;
            value.renderers = reader.readArray(reader => reader.readString()!); if (count == 1) return value;
            value.resolutions = reader.readArray(reader => reader.readString()!); if (count == 2) return value;
            value.displayModes = reader.readArray(reader => reader.readString()!); if (count == 3) return value;
            value.antialiasModes = reader.readArray(reader => reader.readString()!); if (count == 4) return value;
            value.shadowCascadeLevels = reader.readArray(reader => reader.readString()!); if (count == 5) return value;
            value.shadowResolutions = reader.readArray(reader => reader.readString()!); if (count == 6) return value;
            value.renderDistanceNames = reader.readArray(reader => reader.readString()!); if (count == 7) return value;

        }
        return value;
    }

    static deserializeArray(buffer: ArrayBuffer): (AvailableOptions | null)[] | null {
        return this.deserializeArrayCore(new MemoryPackReader(buffer));
    }

    static deserializeArrayCore(reader: MemoryPackReader): (AvailableOptions | null)[] | null {
        return reader.readArray(reader => AvailableOptions.deserializeCore(reader));
    }
}
