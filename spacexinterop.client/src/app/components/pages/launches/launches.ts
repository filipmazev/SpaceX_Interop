import { MatCellDef, MatColumnDef, MatHeaderCellDef, MatHeaderRowDef, MatNoDataRow, MatRowDef, MatTableModule } from '@angular/material/table';
import { Component, OnInit } from '@angular/core';
import { BaseMatTableComponent } from '../../../shared/classes/ui/base-mat-table-component';
import { LaunchRow } from '../../../shared/classes/ui/view/launch-row';
import { SpaceXService } from '../../../shared/services/client/spacex.service';
import { SpaceXLaunchesRequest } from '../../../shared/classes/models/requests/SpaceXLaunchesRequest.model';
import { SortDirectionEnum } from '../../../shared/enums/api/SortDirectionEnum';
import { LaunchRowResponse } from '../../../shared/classes/models/responses/LaunchRowResponse.model';
import { MatPaginator } from '@angular/material/paginator';
import { MatFormField, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { MatOption, MatSelect } from '@angular/material/select';
import { FormsModule } from '@angular/forms';
import { NgClass } from '@angular/common';
import { MatIcon } from '@angular/material/icon';
import { MatMenu, MatMenuTrigger } from '@angular/material/menu';
import { debounceTime, distinctUntilChanged, Subject, takeUntil } from 'rxjs';
import { WindowDimensionsService } from '../../../shared/services/core/ui/window-dimension.service';
import { WindowDimensions } from '../../../shared/interfaces/services/window-dimensions.interface';
import { BASE_DEBOUNCE_TIME_IN_MS, EMPTY_STRING } from '../../../shared/constants/common.constants';
import { Router } from '@angular/router';
import { TableStateService } from '../../../shared/services/core/ui/table-state.service';
import { ITableState } from '../../../shared/interfaces/ui/itable-state.interface';

@Component({
  selector: 'app-launches',
  imports: [
    MatColumnDef,
    MatCellDef,
    MatRowDef,
    MatHeaderCellDef,
    MatHeaderRowDef,
    MatNoDataRow,
    MatPaginator,
    MatTableModule,
    MatLabel,
    MatFormField,
    MatInput,
    MatSelect,
    MatOption,
    FormsModule,
    NgClass,
    MatIcon,
    MatMenu,
    MatMenuTrigger
  ],
  templateUrl: './launches.html',
  styleUrl: './launches.scss'
})
export class Launches extends BaseMatTableComponent<LaunchRow> implements OnInit {
  protected tableId: string = 'launches_table';

  protected displayedColumns: string[] = ['icon', 'name', 'rocketName', 'launchpadName', 'launchDateUtc', 'links', 'status', 'actions'];

  protected SortDirectionEnum = SortDirectionEnum;
  protected searchText: string = EMPTY_STRING;

  protected showUpcomingOnly: boolean = false;
  protected sortOrder: SortDirectionEnum = SortDirectionEnum.Descending;

  private data: LaunchRowResponse[] = [];
  protected filteredData: LaunchRow[] = [];

  protected windowDimensions: WindowDimensions = {} as WindowDimensions;

  private searchTextChanged$ = new Subject<string>();

  constructor(
    private spaceXService: SpaceXService,
    private windowDimensionsService: WindowDimensionsService,
    private router: Router,
    tableStateService: TableStateService
  ) {
    super(tableStateService);
  }

  public ngOnInit(): void {
    this.createSubscriptions();
  }

  public override onDestroy(): void {
    this.searchTextChanged$.complete();
    this.searchTextChanged$.unsubscribe();
  }

  private createSubscriptions(): void {
    this.windowDimensionsService.getWindowDimensions$().pipe(takeUntil(this.unsubscribe$)).subscribe(dimensions => {
      this.windowDimensions = dimensions;
    });

    this.searchTextChanged$
      .pipe(
        debounceTime(BASE_DEBOUNCE_TIME_IN_MS),        
        distinctUntilChanged(),    
        takeUntil(this.unsubscribe$)
      )
      .subscribe(value => {
        this.searchText = value;
        this.fetchData(); 
      });
  }

  protected override async dataFetchingMethod(): Promise<{ data: LaunchRow[]; totalRows?: number; }> {
    return new Promise(async (resolve, reject) => {
      const request: SpaceXLaunchesRequest = {
        searchText: this.searchText,
        upcoming: this.showUpcomingOnly,
        sortDirection: this.sortOrder,
        pageIndex: this.pageIndex(),
        pageSize: this.pageSize()
      };

      await this.spaceXService.getLaunchRows(request).then((result) => {
        if (result.isSuccess && result.value?.items) {
          this.data = result.value.items;
          const rows = this.resolveRowsFromData(result.value?.items);
          this.filteredData = rows;
          resolve({
            data: rows,
            totalRows: result.value.totalItems
          });
        } else {
          reject({ data: [] });
        }
      }).catch((error) => {
        reject(error);
      });
    });
  }

  protected override restoreAdditionalState(state: ITableState) {
    this.searchText = state.searchText ?? EMPTY_STRING;
    this.sortOrder = state.sortOrder ?? SortDirectionEnum.Descending;
    this.showUpcomingOnly = state.showUpcomingOnly ?? false;
  }

  protected override saveState(extra: Partial<ITableState> = {}) {
    super.saveState({
      searchText: this.searchText,
      sortOrder: this.sortOrder,
      showUpcomingOnly: this.showUpcomingOnly,
      ...extra
    });
  }

  private resolveRowsFromData(data: LaunchRowResponse[]): LaunchRow[] {
    return data.map(item => {
      const formattedDate = new Date(item.launchDateUtc).toLocaleDateString("en-US", {
        month: "short",
        day: "numeric",
        year: "numeric",
        hour: "2-digit",
        minute: "2-digit"
      });

      return new LaunchRow(
        item.links?.missionPatchImageSmall,
        item.name,
        item.rocketName,
        item.launchpadName,
        formattedDate,
        item.upcoming,
        item.success,
        item.links?.webcastUrl,
        item.links?.wikipediaUrl,
        item.links?.articleUrl
      );
    });
  }

  //#region UI Methods

  protected viewDetails(index: number) {
    const selectedLaunch = this.data[index];
    if (!selectedLaunch) return;

    this.router.navigate(['/launch', selectedLaunch.id]);
  }

  protected applyFilter(event: Event) {
    const value = (event.target as HTMLInputElement).value.trim();
    this.searchText = value;
    this.saveState(); 
    this.searchTextChanged$.next(value);
  }

  //#endregion
}