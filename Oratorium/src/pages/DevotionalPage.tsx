import { useParams } from 'react-router-dom'
import { useDevotional } from '../hooks/useDevotional'
import { LiturgicalHeader } from '../components/LiturgicalHeader'
import { DateNav } from '../components/DateNav'
import { SaintCard } from '../components/SaintCard'
import { ReadingsList } from '../components/ReadingsList'
import { HomilyCard } from '../components/HomilyCard'
import { LoadingState, ErrorState } from '../components/StatusStates'
import { isValidIsoDate, todayISO } from '../lib/date'

/** Página do devocional de um dia — usada tanto por "/hoje" (`date` de
 * rota indefinido, pede a data atual à API) quanto por "/dia/:date". */
export function DevotionalPage() {
  const { date } = useParams<{ date?: string }>()
  const { data, loading, slow, error, retry } = useDevotional(date)

  const displayDate = data?.date ?? (date && isValidIsoDate(date) ? date : todayISO())

  return (
    <div className="mx-auto flex max-w-2xl flex-col gap-4 px-4 py-6">
      <DateNav currentDate={displayDate} />

      {loading && <LoadingState slow={slow} />}
      {error && !loading && <ErrorState error={error} onRetry={retry} />}

      {data && !loading && !error && (
        <>
          <LiturgicalHeader
            date={data.date}
            liturgicalTitle={data.liturgicalTitle}
            liturgicalColor={data.liturgicalColor}
          />
          {data.saint && <SaintCard saint={data.saint} />}
          <ReadingsList readings={data.readings} />
          {data.homily && <HomilyCard homily={data.homily} />}
        </>
      )}
    </div>
  )
}
