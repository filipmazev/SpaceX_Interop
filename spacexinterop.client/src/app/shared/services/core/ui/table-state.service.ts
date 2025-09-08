import { Injectable } from '@angular/core';
import { ITableState } from '../../../interfaces/ui/itable-state.interface';
import { DbStores } from '../../../enums/db/db-stores.enum';
import { DbService } from '../db.service';
import { environment } from '../../../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class TableStateService {
    constructor(private dbService: DbService) { }

    public async getState(tableId: string): Promise<ITableState | undefined> {
        try {
            return await this.dbService.fetchData(DbStores.TABLE_STATES, tableId);
        } catch (err) {
            if (!environment.production) {
                console.error(`Failed to load table state for ${tableId}`, err);
            }
            return undefined;
        }
    }

    public async setState(tableId: string, state: ITableState): Promise<void> {
        try {
            await this.dbService.saveData(DbStores.TABLE_STATES, tableId, state);
        } catch (err) {
            if (!environment.production) {
                console.error(`Failed to save table state for ${tableId}`, err);
            }
        }
    }

    public async clearState(tableId: string): Promise<void> {
        try {
            await this.dbService.saveData(DbStores.TABLE_STATES, tableId, undefined);
        } catch (err) {
            if (!environment.production) {
                console.error(`Failed to clear table state for ${tableId}`, err);
            }
        }
    }
}