import { SortDirectionEnum } from "../../enums/api/SortDirectionEnum";

export interface ITableState {
  pageIndex: number;
  pageSize: number;
  searchText?: string;
  sortOrder?: SortDirectionEnum;
  showUpcomingOnly?: boolean; 
}