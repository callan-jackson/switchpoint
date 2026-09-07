import { useMemo, useState, type ReactNode } from 'react'
import { ArrowDown, ArrowUp, ArrowUpDown } from 'lucide-react'
import { cn } from '@/lib/cn'
import { EmptyState } from './EmptyState'
import { SkeletonRows } from './Skeleton'

export type SortValue = string | number | boolean | null | undefined

export interface Column<T> {
  id: string
  header: ReactNode
  cell: (row: T, index: number) => ReactNode
  /** Provide to make the column sortable. */
  sortValue?: (row: T) => SortValue
  align?: 'left' | 'right' | 'center'
  className?: string
  headerClassName?: string
  /** Hide the column below a breakpoint. */
  hideBelow?: 'sm' | 'md' | 'lg' | 'xl'
  width?: string
}

export interface SortState {
  columnId: string
  direction: 'asc' | 'desc'
}

export interface DataTableProps<T> {
  columns: Column<T>[]
  rows: T[] | undefined
  getRowId: (row: T, index: number) => string
  caption?: ReactNode
  captionHidden?: boolean
  isLoading?: boolean
  emptyTitle?: ReactNode
  emptyDescription?: ReactNode
  emptyAction?: ReactNode
  onRowClick?: (row: T) => void
  rowClassName?: (row: T) => string | undefined
  initialSort?: SortState
  sort?: SortState
  onSortChange?: (sort: SortState | undefined) => void
  dense?: boolean
  footer?: ReactNode
  className?: string
  /** Selected row ids for highlight. */
  selectedIds?: Set<string>
}

const hideClasses = {
  sm: 'hidden sm:table-cell',
  md: 'hidden md:table-cell',
  lg: 'hidden lg:table-cell',
  xl: 'hidden xl:table-cell',
}

function compare(a: SortValue, b: SortValue): number {
  if (a === b) return 0
  if (a === null || a === undefined) return 1
  if (b === null || b === undefined) return -1
  if (typeof a === 'number' && typeof b === 'number') return a - b
  if (typeof a === 'boolean' && typeof b === 'boolean') return a === b ? 0 : a ? -1 : 1
  return String(a).localeCompare(String(b), 'en-GB', { numeric: true, sensitivity: 'base' })
}

/**
 * Generic table with client-side sorting, loading and empty states. Sorting is stable and null
 * values sort last in both directions. Wide tables scroll inside the component, never the page.
 */
export function DataTable<T>({
  columns,
  rows,
  getRowId,
  caption,
  captionHidden = true,
  isLoading,
  emptyTitle = 'Nothing to show',
  emptyDescription,
  emptyAction,
  onRowClick,
  rowClassName,
  initialSort,
  sort: controlledSort,
  onSortChange,
  dense,
  footer,
  className,
  selectedIds,
}: DataTableProps<T>) {
  const [internalSort, setInternalSort] = useState<SortState | undefined>(initialSort)
  const sort = controlledSort ?? internalSort

  const setSort = (next: SortState | undefined) => {
    if (onSortChange) onSortChange(next)
    if (controlledSort === undefined) setInternalSort(next)
  }

  const toggleSort = (column: Column<T>) => {
    if (!column.sortValue) return
    if (sort?.columnId !== column.id) setSort({ columnId: column.id, direction: 'asc' })
    else if (sort.direction === 'asc') setSort({ columnId: column.id, direction: 'desc' })
    else setSort(undefined)
  }

  const sorted = useMemo(() => {
    if (!rows) return []
    if (!sort) return rows
    const column = columns.find((c) => c.id === sort.columnId)
    if (!column?.sortValue) return rows
    const sortValue = column.sortValue
    const indexed = rows.map((row, index) => ({ row, index, key: sortValue(row) }))
    indexed.sort((a, b) => {
      const c = compare(a.key, b.key)
      if (c !== 0) {
        const nullish = (v: SortValue) => v === null || v === undefined
        if (nullish(a.key) || nullish(b.key)) return c
        return sort.direction === 'asc' ? c : -c
      }
      return a.index - b.index
    })
    return indexed.map((i) => i.row)
  }, [rows, sort, columns])

  if (isLoading && !rows) return <SkeletonRows cols={Math.min(columns.length, 5)} />

  if (!rows || rows.length === 0) {
    return <EmptyState title={emptyTitle} description={emptyDescription} action={emptyAction} />
  }

  return (
    <div className={cn('overflow-x-auto', className)}>
      <table className={cn('table', onRowClick && 'table-hover', dense && 'table-dense')}>
        {caption && <caption className={cn('text-left text-sm text-fg-muted', captionHidden ? 'sr-only' : 'px-3 py-2')}>{caption}</caption>}
        <thead>
          <tr>
            {columns.map((column) => {
              const sortable = !!column.sortValue
              const active = sort?.columnId === column.id
              const ariaSort = active ? (sort.direction === 'asc' ? 'ascending' : 'descending') : sortable ? 'none' : undefined
              return (
                <th
                  key={column.id}
                  scope="col"
                  aria-sort={ariaSort}
                  style={column.width ? { width: column.width } : undefined}
                  className={cn(
                    column.align === 'right' && 'text-right',
                    column.align === 'center' && 'text-center',
                    column.hideBelow && hideClasses[column.hideBelow],
                    column.headerClassName,
                  )}
                >
                  {sortable ? (
                    <button
                      type="button"
                      onClick={() => toggleSort(column)}
                      className={cn(
                        'inline-flex items-center gap-1 rounded hover:text-fg',
                        column.align === 'right' && 'flex-row-reverse',
                        active && 'text-primary-700',
                      )}
                    >
                      {column.header}
                      <span aria-hidden="true" className="[&>svg]:size-3">
                        {active ? sort.direction === 'asc' ? <ArrowUp /> : <ArrowDown /> : <ArrowUpDown className="opacity-50" />}
                      </span>
                    </button>
                  ) : (
                    column.header
                  )}
                </th>
              )
            })}
          </tr>
        </thead>
        <tbody>
          {sorted.map((row, index) => {
            const id = getRowId(row, index)
            const selected = selectedIds?.has(id)
            return (
              <tr
                key={id}
                data-row-id={id}
                onClick={onRowClick ? () => onRowClick(row) : undefined}
                onKeyDown={
                  onRowClick
                    ? (e) => {
                        if (e.key === 'Enter' && e.target === e.currentTarget) onRowClick(row)
                      }
                    : undefined
                }
                tabIndex={onRowClick ? 0 : undefined}
                aria-selected={selectedIds ? selected : undefined}
                className={cn(onRowClick && 'cursor-pointer', selected && 'bg-accent-50', rowClassName?.(row))}
              >
                {columns.map((column) => (
                  <td
                    key={column.id}
                    className={cn(
                      column.align === 'right' && 'num',
                      column.align === 'center' && 'text-center',
                      column.hideBelow && hideClasses[column.hideBelow],
                      column.className,
                    )}
                  >
                    {column.cell(row, index)}
                  </td>
                ))}
              </tr>
            )
          })}
        </tbody>
        {footer && <tfoot>{footer}</tfoot>}
      </table>
    </div>
  )
}

export interface PaginationProps {
  page: number
  pageSize: number
  total: number
  onPageChange: (page: number) => void
}

/** Simple previous/next paginator for `PagedResult` endpoints. */
export function Pagination({ page, pageSize, total, onPageChange }: PaginationProps) {
  const pages = Math.max(1, Math.ceil(total / pageSize))
  const from = total === 0 ? 0 : (page - 1) * pageSize + 1
  const to = Math.min(total, page * pageSize)
  return (
    <nav aria-label="Pagination" className="flex items-center justify-between gap-3 px-1 py-2 text-sm text-fg-muted">
      <p>
        Showing <span className="font-medium text-fg">{from}–{to}</span> of <span className="font-medium text-fg">{total}</span>
      </p>
      <div className="flex items-center gap-2">
        <button
          type="button"
          onClick={() => onPageChange(page - 1)}
          disabled={page <= 1}
          className="rounded-md border border-border-strong px-2.5 py-1 hover:bg-surface-muted disabled:opacity-50"
        >
          Previous
        </button>
        <span aria-current="page">
          Page {page} of {pages}
        </span>
        <button
          type="button"
          onClick={() => onPageChange(page + 1)}
          disabled={page >= pages}
          className="rounded-md border border-border-strong px-2.5 py-1 hover:bg-surface-muted disabled:opacity-50"
        >
          Next
        </button>
      </div>
    </nav>
  )
}
