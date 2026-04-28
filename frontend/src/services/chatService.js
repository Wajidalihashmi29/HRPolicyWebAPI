const API_BASE_URL = 'http://localhost:5157';

export const chatService = {
  async *streamChat(message) {
    try {
      const response = await fetch(`${API_BASE_URL}/api/hragent/chat`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ message }),
      });

      if (!response.ok) {
        throw new Error(`API error: ${response.statusText}`);
      }

      const reader = response.body.getReader();
      const decoder = new TextDecoder();
      let buffer = '';
      let eventData = '';

      const flushEvent = () => {
        if (!eventData) return null;
        try {
          const event = JSON.parse(eventData);
          eventData = '';
          if (event.type === 'text' && event.text) return { type: 'text', text: event.text };
          if (event.type === 'citation' && event.text) return { type: 'citation', text: event.text };
          if (event.type === 'error') throw new Error(event.text);
          if (event.type === 'done') return { type: 'done' };
        } catch (parseError) {
          console.error('Failed to parse SSE event:', eventData, parseError);
        }
        return null;
      };

      while (true) {
        const { done, value } = await reader.read();
        if (done) break;

        buffer += decoder.decode(value, { stream: true });
        let lineEndIndex;

        while ((lineEndIndex = buffer.indexOf('\n')) !== -1) {
          const line = buffer.slice(0, lineEndIndex).trim();
          buffer = buffer.slice(lineEndIndex + 1);

          if (!line) {
            const event = flushEvent();
            if (event) {
              if (event.type === 'done') return;
              yield event;
            }
            continue;
          }

          if (line.startsWith('data:')) {
            eventData += line.slice(5).trim();
          }
        }
      }

      if (buffer.trim()) {
        const line = buffer.trim();
        if (line.startsWith('data:')) {
          eventData += line.slice(5).trim();
        }
      }

      const finalEvent = flushEvent();
      if (finalEvent) {
        if (finalEvent.type === 'done') return;
        yield finalEvent;
      }
    } catch (error) {
      console.error('Chat error:', error);
      throw error;
    }
  },
};