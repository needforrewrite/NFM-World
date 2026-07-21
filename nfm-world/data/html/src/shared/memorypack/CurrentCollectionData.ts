import { MemoryPackWriter } from "./MemoryPackWriter";
import { MemoryPackReader } from "./MemoryPackReader";
import { Collection } from "./Collection";

export class CurrentCollectionData {
    id: Collection;

    constructor() {
        this.id = 0;

    }

    static serialize(value: CurrentCollectionData | null): Uint8Array {
        const writer = MemoryPackWriter.getSharedInstance();
        this.serializeCore(writer, value);
        return writer.toArray();
    }

    static serializeCore(writer: MemoryPackWriter, value: CurrentCollectionData | null): void {
        if (value == null) {
            writer.writeNullObjectHeader();
            return;
        }

        writer.writeObjectHeader(1);
        writer.writeInt32(value.id);

    }

    static serializeArray(value: (CurrentCollectionData | null)[] | null): Uint8Array {
        const writer = MemoryPackWriter.getSharedInstance();
        this.serializeArrayCore(writer, value);
        return writer.toArray();
    }

    static serializeArrayCore(writer: MemoryPackWriter, value: (CurrentCollectionData | null)[] | null): void {
        writer.writeArray(value, (writer, x) => CurrentCollectionData.serializeCore(writer, x));
    }

    static deserialize(buffer: ArrayBuffer): CurrentCollectionData | null {
        return this.deserializeCore(new MemoryPackReader(buffer));
    }

    static deserializeCore(reader: MemoryPackReader): CurrentCollectionData | null {
        const [ok, count] = reader.tryReadObjectHeader();
        if (!ok) {
            return null;
        }

        const value = new CurrentCollectionData();
        if (count == 1) {
            value.id = reader.readInt32()!;

        }
        else if (count > 1) {
            throw new Error("Current object's property count is larger than type schema, can't deserialize about versioning.");
        }
        else {
            if (count == 0) return value;
            value.id = reader.readInt32()!; if (count == 1) return value;

        }
        return value;
    }

    static deserializeArray(buffer: ArrayBuffer): (CurrentCollectionData | null)[] | null {
        return this.deserializeArrayCore(new MemoryPackReader(buffer));
    }

    static deserializeArrayCore(reader: MemoryPackReader): (CurrentCollectionData | null)[] | null {
        return reader.readArray(reader => CurrentCollectionData.deserializeCore(reader));
    }
}
