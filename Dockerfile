FROM python:slim

RUN pip install pipenv

WORKDIR /var/app/scraper
COPY scraper/Pipfil* ./
COPY scraper/setup.py ./
WORKDIR /var/app/server
COPY server/Pipfil* ./
RUN pipenv install
RUN pipenv graph

WORKDIR /var/app/scraper
COPY scraper .
WORKDIR /var/app/server
COPY server .
RUN rm server/scraper
RUN ln -s /var/app/scraper ./server/scraper

ENV PORT 5000
EXPOSE ${PORT}

CMD ["pipenv", "run", "python3", "-m", "main"]
