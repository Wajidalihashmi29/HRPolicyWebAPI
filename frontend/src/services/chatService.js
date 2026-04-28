const API_BASE_URL = 'http://localhost:5157';

export const chatService = {
  async *streamChat(message) {
    console.log('chatService: sending message', message);
    const response = await fetch(`${API_BASE_URL}/api/hragent/chat`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Accept': 'text/event-stream',
      },
      body: JSON.stringify({ message }),
    });

    if (!response.ok) {
      throw new Error(`API error: ${response.status} ${response.statusText}`);
    }

    let eventData = '';
    const flushEvent = () => {
      if (!eventData) return null;
      try {
        const event = JSON.parse(eventData);
        eventData = '';

        if (event.type === 'done') return { type: 'done' };
        if (event.type === 'text' || event.type === 'citation') return event;
        if (event.type === 'error') throw new Error(event.text ?? 'Unknown error');
      } catch (parseError) {
        console.error('Failed to parse SSE event:', eventData, parseError);
        eventData = '';
      }
      return null;
    };

    const tryParseJsonArray = (chunk) => {
      const raw = chunk.trim();
      if (!raw.startsWith('[') || !raw.endsWith(']')) return null;
      try {
        const items = JSON.parse(raw);
        if (Array.isArray(items) && items.every(item => typeof item === 'string')) {
          return items;
        }
      } catch {
        return null;
      }
      return null;
    };

    const processEventChunk = (chunk) => {
      const rawEvent = chunk.trim();
      if (!rawEvent) return null;

      const lines = rawEvent.split(/\r?\n/);
      lines.forEach((line) => {
        if (line.startsWith('data:')) {
          eventData += line.slice(5).trim();
        }
      });

      return flushEvent();
    };

    const reader = response.body?.getReader();
    if (!reader) {
      const fallbackText = await response.text();
      console.log('chatService: fallback full response text', fallbackText);
      const chunks = fallbackText.split(/\r?\n\r?\n/);
      for (const chunk of chunks) {
        const jsonItems = tryParseJsonArray(chunk);
        if (jsonItems) {
          for (const item of jsonItems) {
            const event = processEventChunk(item);
            if (event) {
              if (event.type === 'done') return;
              console.log('chatService parsed event (fallback)', event);
              yield event;
            }
          }
        } else {
          const event = processEventChunk(chunk);
          if (event) {
            if (event.type === 'done') return;
            console.log('chatService parsed event (fallback)', event);
            yield event;
          }
        }
      }
      return;
    }

    const decoder = new TextDecoder('utf-8');
    let buffer = '';

    const findBoundary = (text) => {
      const rnIndex = text.indexOf('\r\n\r\n');
      if (rnIndex !== -1) return { index: rnIndex, length: 4 };
      const nIndex = text.indexOf('\n\n');
      if (nIndex !== -1) return { index: nIndex, length: 2 };
      return null;
    };

    while (true) {
      const { done, value } = await reader.read();
      if (done) break;

      const chunkText = decoder.decode(value, { stream: true });
      console.log('chatService: chunkText', JSON.stringify(chunkText));
      buffer += chunkText;

      const jsonItems = tryParseJsonArray(buffer);
      if (jsonItems) {
        buffer = '';
        for (const item of jsonItems) {
          const event = processEventChunk(item);
          if (event) {
            if (event.type === 'done') return;
            console.log('chatService parsed event', event);
            yield event;
          }
        }
        continue;
      }

      let boundary;
      while ((boundary = findBoundary(buffer)) !== null) {
        const chunk = buffer.slice(0, boundary.index);
        buffer = buffer.slice(boundary.index + boundary.length);

        const event = processEventChunk(chunk);
        if (event) {
          if (event.type === 'done') return;
          console.log('chatService parsed event', event);
          yield event;
        }
      }
    }

    if (buffer.trim()) {
      const jsonItems = tryParseJsonArray(buffer);
      if (jsonItems) {
        for (const item of jsonItems) {
          const event = processEventChunk(item);
          if (event && event.type !== 'done') {
            yield event;
          }
        }
      } else {
        const event = processEventChunk(buffer);
        if (event && event.type !== 'done') {
          yield event;
        }
      }
    }
  },
};