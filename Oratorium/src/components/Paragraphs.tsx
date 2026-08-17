interface ParagraphsProps {
  text: string
  className?: string
  /** Destaca em negrito números de versículo "soltos" no meio do texto (ex:
   * "39 Maria partiu..."), como o site-fonte já faz visualmente. O backend
   * guarda só texto puro (sem a formatação HTML original), então isso é
   * recriado aqui com uma heurística: qualquer número de 1-3 dígitos isolado
   * por espaços. Usar só em texto bíblico (leituras) — em prosa comum
   * (biografia do santo, homilia) um número solto raramente é um versículo,
   * então não vale o risco de negritar algo errado. */
  highlightVerseNumbers?: boolean
}

const VERSE_NUMBER_PATTERN = /(?<=^|\s)(\d{1,3})(?=\s)/g

/** Quebra um parágrafo em trechos alternando texto comum e números de
 * versículo, preservando a ordem original — usado para poder envolver só os
 * números num <strong>, sem precisar de dangerouslySetInnerHTML. */
function splitVerseNumbers(text: string): Array<{ text: string; isVerseNumber: boolean }> {
  const segments: Array<{ text: string; isVerseNumber: boolean }> = []
  let lastIndex = 0

  for (const match of text.matchAll(VERSE_NUMBER_PATTERN)) {
    const index = match.index ?? 0
    if (index > lastIndex) {
      segments.push({ text: text.slice(lastIndex, index), isVerseNumber: false })
    }
    segments.push({ text: match[0], isVerseNumber: true })
    lastIndex = index + match[0].length
  }

  if (lastIndex < text.length) {
    segments.push({ text: text.slice(lastIndex), isVerseNumber: false })
  }

  return segments
}

/** O backend separa parágrafos com linhas em branco (\n\n) — este componente
 * converte isso em elementos <p> reais, em vez de depender de `white-space:
 * pre-wrap` (que preservaria espaçamento irregular vindo do HTML original). */
export function Paragraphs({ text, className = '', highlightVerseNumbers = false }: ParagraphsProps) {
  const paragraphs = text
    .split(/\n{2,}/)
    .map((p) => p.trim())
    .filter(Boolean)

  return (
    <div className={`space-y-4 ${className}`}>
      {paragraphs.map((paragraph, index) =>
        highlightVerseNumbers ? (
          <p key={index}>
            {splitVerseNumbers(paragraph).map((segment, segmentIndex) =>
              segment.isVerseNumber ? (
                <strong key={segmentIndex} className="font-semibold text-neutral-900 dark:text-neutral-100">
                  {segment.text}
                </strong>
              ) : (
                <span key={segmentIndex}>{segment.text}</span>
              ),
            )}
          </p>
        ) : (
          <p key={index}>{paragraph}</p>
        ),
      )}
    </div>
  )
}
