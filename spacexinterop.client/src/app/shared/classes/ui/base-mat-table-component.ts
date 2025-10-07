import { AfterViewInit, Directive, OnDestroy, signal, ViewChild, WritableSignal } from "@angular/core";
import { MatPaginator, PageEvent } from "@angular/material/paginator";
import { MatTableDataSource } from "@angular/material/table";
import { MatIcons } from "../../enums/ui/mat-icons.enum";
import { Subject, takeUntil } from "rxjs";
import { TableStateService } from "../../services/core/ui/table-state.service";
import { ITableState } from "../../interfaces/ui/itable-state.interface";
import * as commonConst from "../../constants/common.constants";

@Directive()
export abstract class BaseMatTableComponent<TData extends object = any> implements AfterViewInit, OnDestroy {
    protected abstract tableId: string;

    protected dataSource: MatTableDataSource<TData> = new MatTableDataSource<TData>();

    protected readonly pageSizeOptions: number[] = [commonConst.TABLE_PAGE_SIZE_OPTION_1, commonConst.TABLE_PAGE_SIZE_OPTION_2, commonConst.TABLE_PAGE_SIZE_OPTION_3, commonConst.TABLE_PAGE_SIZE_OPTION_4];

    protected pageIndex: WritableSignal<number> = signal(0);
    protected pageSize: WritableSignal<number> = signal(this.pageSizeOptions[1]);
    protected totalRows: WritableSignal<number> = signal(0);

    protected isFetching: boolean = false;

    protected menuOpenState: boolean[] = []; 
    
    protected abstract displayedColumns: string[];

    protected readonly EMPTY_TABLE_STRING: string = commonConst.EMPTY_TABLE_STRING;
    protected readonly EMPTY_TABLE_FIELD_STRING: string = commonConst.EMPTY_TABLE_FIELD_STRING;

    protected readonly unsubscribe$ = new Subject<void>();

    protected MatIcons = MatIcons;

    @ViewChild(MatPaginator) paginator!: MatPaginator;

    constructor(protected tableStateService: TableStateService) {
    }

    public async ngAfterViewInit(): Promise<void> {
        const state = await this.tableStateService.getState(this.tableId);
        if (state) {
            this.pageIndex.set(state.pageIndex ?? 0);
            this.pageSize.set(state.pageSize ?? this.pageSizeOptions[1]);
            this.restoreAdditionalState(state);
        }

        this.paginator.pageSize = this.pageSize();
        this.paginator.pageIndex = this.pageIndex();
        this.fetchData();

        this.paginator.page.pipe(takeUntil(this.unsubscribe$)).subscribe(event => {
            this.changePage(event);
            this.saveState();
        });
    }

    public ngOnDestroy(): void {
        this.unsubscribe$.next();
        this.unsubscribe$.complete();

        this.onDestroy();
        this.saveState(); 
    }

    protected saveState(extraState: Partial<ITableState> = {}) {
        const state: ITableState = {
            pageIndex: this.pageIndex(),
            pageSize: this.pageSize(),
            ...extraState
        };
        this.tableStateService.setState(this.tableId, state);
    }

    protected restoreAdditionalState(_state: ITableState) {
    }

    protected abstract dataFetchingMethod(): Promise<{ data: TData[], totalRows?: number }>;
    protected abstract onDestroy(): void;
    
    protected async fetchData(): Promise<TData[]> {
        return new Promise<TData[]>((resolve, reject) => {
            if(this.isFetching) { resolve([]); return; }

            this.isFetching = true;
            this.dataSource.data = [];

            this.dataFetchingMethod()
                .then(response => {
                    this.dataSource.data = response.data;
                    if (response.totalRows !== undefined) {
                        this.totalRows.set(response.totalRows);
                        this.paginator.length = response.totalRows; 
                    } else {
                        this.dataSource.paginator = this.paginator;
                    }
                    resolve(response.data);
                })
                .catch(error => {
                    reject(error);
                }).finally(() =>{
                    this.isFetching = false;
                });
        });
    }

    public changePage(event: PageEvent): void {
        const newPageSize = event.pageSize;
        const newPageIndex = event.pageIndex;

        if (newPageSize !== this.pageSize()) {
            this.pageSize.set(newPageSize);
            this.pageIndex.set(0);
            this.paginator.firstPage();
            this.saveState();
        } else {
            this.pageIndex.set(newPageIndex);
        }

        this.fetchData();
    }
}