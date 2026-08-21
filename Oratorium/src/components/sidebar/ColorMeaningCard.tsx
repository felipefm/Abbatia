import { Card } from '../Card'
import { getLiturgicalColorTheme } from '../../lib/liturgicalColor'
import type { LiturgicalColor } from '../../api/types'

const COLOR_MEANINGS: Record<LiturgicalColor, string> = {
  Verde: 'Tempo Comum — o "dia a dia" da vida cristã, fora dos grandes ciclos do ano litúrgico.',
  Roxo: 'Advento e Quaresma — tempos de preparação, penitência e conversão.',
  Branco: 'Festas do Senhor, de Nossa Senhora e dos santos não-mártires; usado também na Páscoa e no Natal.',
  Vermelho: 'Paixão do Senhor, festas de mártires, e Pentecostes/Espírito Santo.',
  Rosa: 'Domingo Gaudete (3º do Advento) e Laetare (4º da Quaresma) — alegria antecipada em meio à preparação.',
  Preto: 'Missas de defuntos — uso hoje opcional e raro.',
}

export function ColorMeaningCard({ color }: { color: LiturgicalColor }) {
  const theme = getLiturgicalColorTheme(color)

  return (
    <Card eyebrow="Significado da cor">
      <div className="mt-1 flex items-center gap-2">
        <span className="h-3 w-3 rounded-full" style={{ backgroundColor: theme.hex }} />
        <h3 className="font-serif-reading text-lg text-neutral-900 dark:text-neutral-50">{theme.label}</h3>
      </div>
      <p className="mt-2 text-sm leading-relaxed text-neutral-600 dark:text-neutral-400">{COLOR_MEANINGS[color]}</p>
    </Card>
  )
}
