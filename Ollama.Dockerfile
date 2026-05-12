FROM ollama/ollama

COPY ollama-init.sh /usr/local/bin/ollama-init.sh
RUN chmod +x /usr/local/bin/ollama-init.sh

ENTRYPOINT ["/usr/local/bin/ollama-init.sh"]
