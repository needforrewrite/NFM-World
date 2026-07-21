import { MemoryPackWriter } from "./MemoryPackWriter";
import { MemoryPackReader } from "./MemoryPackReader";
import { Collection } from "./Collection";
import { CarStatsData } from "./CarStatsData";

export class CarCollectionData {
    id: Collection;
    name: string;
    cars: (CarStatsData | null)[] | null;

    constructor() {
        this.id = 0;
        this.name = "";
        this.cars = null;

    }

    static serialize(value: CarCollectionData | null): Uint8Array {
        const writer = MemoryPackWriter.getSharedInstance();
        this.serializeCore(writer, value);
        return writer.toArray();
    }

    static serializeCore(writer: MemoryPackWriter, value: CarCollectionData | null): void {
        if (value == null) {
            writer.writeNullObjectHeader();
            return;
        }

        writer.writeObjectHeader(3);
        writer.writeInt32(value.id);
        writer.writeString(value.name);
        writer.writeArray(value.cars, (writer, x) => CarStatsData.serializeCore(writer, x));

    }

    static serializeArray(value: (CarCollectionData | null)[] | null): Uint8Array {
        const writer = MemoryPackWriter.getSharedInstance();
        this.serializeArrayCore(writer, value);
        return writer.toArray();
    }

    static serializeArrayCore(writer: MemoryPackWriter, value: (CarCollectionData | null)[] | null): void {
        writer.writeArray(value, (writer, x) => CarCollectionData.serializeCore(writer, x));
    }

    static deserialize(buffer: ArrayBuffer): CarCollectionData | null {
        return this.deserializeCore(new MemoryPackReader(buffer));
    }

    static deserializeCore(reader: MemoryPackReader): CarCollectionData | null {
        const [ok, count] = reader.tryReadObjectHeader();
        if (!ok) {
            return null;
        }

        const value = new CarCollectionData();
        if (count == 3) {
            value.id = reader.readInt32()!;
            value.name = reader.readString()!;
            value.cars = reader.readArray(reader => CarStatsData.deserializeCore(reader));

        }
        else if (count > 3) {
            throw new Error("Current object's property count is larger than type schema, can't deserialize about versioning.");
        }
        else {
            if (count == 0) return value;
            value.id = reader.readInt32()!; if (count == 1) return value;
            value.name = reader.readString()!; if (count == 2) return value;
            value.cars = reader.readArray(reader => CarStatsData.deserializeCore(reader)); if (count == 3) return value;

        }
        return value;
    }

    static deserializeArray(buffer: ArrayBuffer): (CarCollectionData | null)[] | null {
        return this.deserializeArrayCore(new MemoryPackReader(buffer));
    }

    static deserializeArrayCore(reader: MemoryPackReader): (CarCollectionData | null)[] | null {
        return reader.readArray(reader => CarCollectionData.deserializeCore(reader));
    }
}
