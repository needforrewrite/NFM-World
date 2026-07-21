import { MemoryPackWriter } from "./MemoryPackWriter";
import { MemoryPackReader } from "./MemoryPackReader";

export class KeyBindingData {
    action: string;
    displayName: string;
    keyCode: number;

    constructor() {
        this.action = "";
        this.displayName = "";
        this.keyCode = 0;

    }

    static serialize(value: KeyBindingData | null): Uint8Array {
        const writer = MemoryPackWriter.getSharedInstance();
        this.serializeCore(writer, value);
        return writer.toArray();
    }

    static serializeCore(writer: MemoryPackWriter, value: KeyBindingData | null): void {
        if (value == null) {
            writer.writeNullObjectHeader();
            return;
        }

        writer.writeObjectHeader(3);
        writer.writeString(value.action);
        writer.writeString(value.displayName);
        writer.writeInt32(value.keyCode);

    }

    static serializeArray(value: (KeyBindingData | null)[] | null): Uint8Array {
        const writer = MemoryPackWriter.getSharedInstance();
        this.serializeArrayCore(writer, value);
        return writer.toArray();
    }

    static serializeArrayCore(writer: MemoryPackWriter, value: (KeyBindingData | null)[] | null): void {
        writer.writeArray(value, (writer, x) => KeyBindingData.serializeCore(writer, x));
    }

    static deserialize(buffer: ArrayBuffer): KeyBindingData | null {
        return this.deserializeCore(new MemoryPackReader(buffer));
    }

    static deserializeCore(reader: MemoryPackReader): KeyBindingData | null {
        const [ok, count] = reader.tryReadObjectHeader();
        if (!ok) {
            return null;
        }

        const value = new KeyBindingData();
        if (count == 3) {
            value.action = reader.readString()!;
            value.displayName = reader.readString()!;
            value.keyCode = reader.readInt32()!;

        }
        else if (count > 3) {
            throw new Error("Current object's property count is larger than type schema, can't deserialize about versioning.");
        }
        else {
            if (count == 0) return value;
            value.action = reader.readString()!; if (count == 1) return value;
            value.displayName = reader.readString()!; if (count == 2) return value;
            value.keyCode = reader.readInt32()!; if (count == 3) return value;

        }
        return value;
    }

    static deserializeArray(buffer: ArrayBuffer): (KeyBindingData | null)[] | null {
        return this.deserializeArrayCore(new MemoryPackReader(buffer));
    }

    static deserializeArrayCore(reader: MemoryPackReader): (KeyBindingData | null)[] | null {
        return reader.readArray(reader => KeyBindingData.deserializeCore(reader));
    }
}
